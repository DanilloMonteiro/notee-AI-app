using System.Text.Json;
using System.Threading.Channels;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NoteLab.Application;

namespace NoteLab.Domain
{
    /// <summary>
    /// Transactional Outbox (M11): o evento é gravado na MESMA transação do job e publicado depois.
    /// Assim, nunca existe job sem evento nem evento sem job.
    /// </summary>
    public sealed class OutboxMessage
    {
        public Guid Id { get; private set; }
        public string Type { get; private set; }
        public string Payload { get; private set; }
        public DateTimeOffset OccurredAt { get; private set; }
        public DateTimeOffset? ProcessedAt { get; private set; }
        public int Attempts { get; private set; }
        public string? LastError { get; private set; }

        private OutboxMessage()
        {
            Type = null!;
            Payload = null!;
        }

        public OutboxMessage(string type, object payload)
        {
            Id = Guid.NewGuid();
            Type = type;
            Payload = JsonSerializer.Serialize(payload);
            OccurredAt = DateTimeOffset.UtcNow;
        }

        public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;

        public void MarkFailed(string error)
        {
            Attempts++;
            LastError = error.Length > 300 ? error[..300] : error;
        }
    }

    /// <summary>Inbox: registra cada evento consumido para que duplicatas (entrega at-least-once) sejam ignoradas.</summary>
    public sealed class InboxMessage
    {
        public string Consumer { get; private set; }
        public Guid MessageId { get; private set; }
        public DateTimeOffset ReceivedAt { get; private set; }

        private InboxMessage()
        {
            Consumer = null!;
        }

        public InboxMessage(string consumer, Guid messageId)
        {
            Consumer = consumer;
            MessageId = messageId;
            ReceivedAt = DateTimeOffset.UtcNow;
        }
    }

    public static class OutboxEvents
    {
        public const string ResearchJobEnqueued = "research.job.enqueued";

        public static OutboxMessage JobEnqueued(ResearchJob job) =>
            new(ResearchJobEnqueued, new { jobId = job.Id, doubtId = job.DoubtId });
    }
}

namespace NoteLab.Infrastructure
{
    using NoteLab.Domain;

    /// <summary>Sinal em memória para acordar o worker assim que houver trabalho (sem esperar o polling).</summary>
    public sealed class WorkSignal
    {
        private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

        public void Notify() => _channel.Writer.TryWrite(true);

        /// <summary>Espera um sinal ou o tempo limite (o polling continua sendo a rede de segurança).</summary>
        public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try
            {
                await _channel.Reader.ReadAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // tempo esgotado: segue para o próximo ciclo
            }
        }
    }

    public interface IMessageBus
    {
        bool IsExternal { get; }
        Task PublishAsync(OutboxMessage message, CancellationToken ct);
    }

    /// <summary>Padrão sem broker: a tabela de jobs é a fila; publicar apenas acorda o worker local.</summary>
    public sealed class InProcessMessageBus : IMessageBus
    {
        private readonly WorkSignal _signal;

        public InProcessMessageBus(WorkSignal signal)
        {
            _signal = signal;
        }

        public bool IsExternal => false;

        public Task PublishAsync(OutboxMessage message, CancellationToken ct)
        {
            _signal.Notify();
            return Task.CompletedTask;
        }
    }

    /// <summary>SQS (LocalStack em ambiente local — ADR-009/028). MessageId do SQS não é usado para dedupe; o Id do Outbox é.</summary>
    public sealed class SqsMessageBus : IMessageBus
    {
        private readonly IAmazonSQS _sqs;
        private readonly string _queueUrl;

        public SqsMessageBus(IAmazonSQS sqs, SqsOptions options)
        {
            _sqs = sqs;
            _queueUrl = options.QueueUrl;
        }

        public bool IsExternal => true;

        public Task PublishAsync(OutboxMessage message, CancellationToken ct) =>
            _sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = message.Payload,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["eventId"] = new() { DataType = "String", StringValue = message.Id.ToString() },
                    ["type"] = new() { DataType = "String", StringValue = message.Type },
                },
            }, ct);
    }

    public sealed record SqsOptions(string ServiceUrl, string QueueUrl);

    /// <summary>Publica mensagens pendentes do Outbox (com retry); idempotente do lado do consumidor via Inbox.</summary>
    public sealed class OutboxDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IMessageBus _bus;
        private readonly ILogger<OutboxDispatcher> _logger;

        public OutboxDispatcher(IServiceScopeFactory scopes, IMessageBus bus, ILogger<OutboxDispatcher> logger)
        {
            _scopes = scopes;
            _bus = bus;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DispatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Falha ao publicar Outbox");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        private async Task DispatchAsync(CancellationToken ct)
        {
            using IServiceScope scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Linhas travadas por outra instância são puladas (várias réplicas podem rodar).
            List<OutboxMessage> pending = await db.OutboxMessages
                .FromSql($"""SELECT * FROM "OutboxMessages" WHERE "ProcessedAt" IS NULL AND "Attempts" < 10 ORDER BY "OccurredAt" LIMIT 50 FOR UPDATE SKIP LOCKED""")
                .ToListAsync(ct);

            foreach (OutboxMessage message in pending)
            {
                try
                {
                    await _bus.PublishAsync(message, ct);
                    message.MarkProcessed();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.MarkFailed(ex.GetType().Name);
                    _logger.LogWarning("Outbox {MessageId} não publicado (tentativa {Attempt}): {Error}", message.Id, message.Attempts, ex.GetType().Name);
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }

    /// <summary>
    /// Consumidor SQS: dedupe pelo Inbox, acorda o worker e apaga a mensagem.
    /// O processamento real continua sendo a reivindicação com lease na tabela de jobs.
    /// </summary>
    public sealed class SqsJobListener : BackgroundService
    {
        public const string ConsumerName = "research-worker";

        private readonly IAmazonSQS _sqs;
        private readonly SqsOptions _options;
        private readonly IServiceScopeFactory _scopes;
        private readonly WorkSignal _signal;
        private readonly ILogger<SqsJobListener> _logger;

        public SqsJobListener(IAmazonSQS sqs, SqsOptions options, IServiceScopeFactory scopes, WorkSignal signal, ILogger<SqsJobListener> logger)
        {
            _sqs = sqs;
            _options = options;
            _scopes = scopes;
            _signal = signal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    ReceiveMessageResponse response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
                    {
                        QueueUrl = _options.QueueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 10,
                        MessageAttributeNames = ["All"],
                    }, stoppingToken);

                    foreach (Message message in response.Messages ?? [])
                    {
                        await HandleAsync(message, stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Falha ao ler SQS: {Error}", ex.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private async Task HandleAsync(Message message, CancellationToken ct)
        {
            if (message.MessageAttributes?.TryGetValue("eventId", out MessageAttributeValue? attr) == true
                && Guid.TryParse(attr.StringValue, out Guid eventId))
            {
                using IServiceScope scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NoteDbContext>();
                db.InboxMessages.Add(new InboxMessage(ConsumerName, eventId));
                try
                {
                    await db.SaveChangesAsync(ct);
                    _signal.Notify();
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    _logger.LogInformation("Evento {EventId} duplicado ignorado (Inbox)", eventId);
                }
            }

            // Mensagem tratada (ou sem identificação válida): remove da fila.
            await _sqs.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, ct);
        }
    }

    public static class MessagingExtensions
    {
        /// <summary>
        /// Messaging:Provider = "inprocess" (padrão) ou "sqs" (exige Messaging:Sqs:ServiceUrl e QueueUrl;
        /// pensado para LocalStack — credenciais fictícias).
        /// </summary>
        public static IServiceCollection AddNoteeMessaging(this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<WorkSignal>();
            services.AddHostedService<OutboxDispatcher>();

            if (string.Equals(config["Messaging:Provider"], "sqs", StringComparison.OrdinalIgnoreCase))
            {
                var options = new SqsOptions(
                    config["Messaging:Sqs:ServiceUrl"] ?? throw new InvalidOperationException("Messaging:Sqs:ServiceUrl é obrigatório."),
                    config["Messaging:Sqs:QueueUrl"] ?? throw new InvalidOperationException("Messaging:Sqs:QueueUrl é obrigatório."));
                services.AddSingleton(options);
                services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(
                    new BasicAWSCredentials(config["Messaging:Sqs:AccessKey"] ?? "test", config["Messaging:Sqs:SecretKey"] ?? "test"),
                    new AmazonSQSConfig { ServiceURL = options.ServiceUrl, AuthenticationRegion = "us-east-1" }));
                services.AddSingleton<IMessageBus, SqsMessageBus>();
                services.AddHostedService<SqsJobListener>();
            }
            else
            {
                services.AddSingleton<IMessageBus, InProcessMessageBus>();
            }

            return services;
        }
    }
}
