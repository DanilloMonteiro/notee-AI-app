# Infra AWS do Notee **somente emulada** no LocalStack (ADR-009). Nunca aponta para AWS real:
# credenciais fictícias e endpoint local obrigatório.
terraform {
  required_version = ">= 1.6"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.80"
    }
  }
}

variable "localstack_endpoint" {
  description = "Endpoint do LocalStack (ex.: http://localhost:4566 ou http://host.docker.internal:4566)"
  type        = string
  default     = "http://localhost:4566"
}

provider "aws" {
  region                      = "us-east-1"
  access_key                  = "test"
  secret_key                  = "test"
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true

  endpoints {
    sqs = var.localstack_endpoint
    s3  = var.localstack_endpoint
  }
}

resource "aws_s3_bucket" "source_archives" {
  bucket = "notee-source-archives"
}

resource "aws_s3_bucket_public_access_block" "source_archives" {
  bucket                  = aws_s3_bucket.source_archives.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

output "source_archive_bucket" {
  value = aws_s3_bucket.source_archives.bucket
}

# Mensagens que falharem 5 vezes vão para a DLQ para inspeção manual.
resource "aws_sqs_queue" "research_jobs_dlq" {
  name                      = "notee-research-jobs-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "research_jobs" {
  name                       = "notee-research-jobs"
  visibility_timeout_seconds = 120
  receive_wait_time_seconds  = 20
  message_retention_seconds  = 345600
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.research_jobs_dlq.arn
    maxReceiveCount     = 5
  })
}

output "research_queue_url" {
  value = aws_sqs_queue.research_jobs.url
}

output "research_dlq_url" {
  value = aws_sqs_queue.research_jobs_dlq.url
}
