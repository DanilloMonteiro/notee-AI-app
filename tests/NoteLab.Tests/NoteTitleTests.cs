using Xunit;
using System;
using NoteLab.Domain; 

namespace NoteLab.Tests;

public class NoteTitleTests
{
    [Fact]
    public void Constructor_with_empty_title_throws_exception()
    {
        // Act: Definimos a ação que vai gerar o erro
        Action acao = () => new NoteTitle("");

        // Assert: Verificamos se a ação realmente lança a exceção esperada
        Assert.Throws<ArgumentException>(acao);
    }

    [Fact]
    public void Constructor_with_more_than_100_caracteres_in_title_throws_expection()
    {
        string tituloGigante = new string('A', 101);

        Action acao = () => new NoteTitle(tituloGigante);

        Assert.Throws<ArgumentException>(acao);
    } 
}