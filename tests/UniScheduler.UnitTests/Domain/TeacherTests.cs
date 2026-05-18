using FluentAssertions;
using UniScheduler.Domain.Entities;
using Xunit;

namespace UniScheduler.UnitTests.Domain;

public class TeacherTests
{
    [Fact]
    public void DisplayName_AllParts_JoinsCorrectly()
    {
        var teacher = new Teacher { FirstName = "Иван", LastName = "Иванов", MiddleName = "Иванович" };
        teacher.DisplayName.Should().Be("Иванов Иван Иванович");
    }

    [Fact]
    public void DisplayName_EmptyMiddleName_OmitsExtraSpace()
    {
        var teacher = new Teacher { FirstName = "Иван", LastName = "Иванов", MiddleName = "" };
        teacher.DisplayName.Should().Be("Иванов Иван");
    }

    [Fact]
    public void DisplayName_OnlyLastName_ReturnsSingleToken()
    {
        var teacher = new Teacher { FirstName = "", LastName = "Иванов", MiddleName = "" };
        teacher.DisplayName.Should().Be("Иванов");
    }

    [Fact]
    public void ShortName_AllParts_ProducesInitials()
    {
        var teacher = new Teacher { FirstName = "Иван", LastName = "Иванов", MiddleName = "Иванович" };
        teacher.ShortName.Should().Be("Иванов И. И.");
    }

    [Fact]
    public void ShortName_EmptyFirstName_FallsBackToDisplayName()
    {
        var teacher = new Teacher { FirstName = "", LastName = "Иванов", MiddleName = "Иванович" };
        teacher.ShortName.Should().Be(teacher.DisplayName);
    }

    [Fact]
    public void ShortName_EmptyMiddleName_FallsBackToDisplayName()
    {
        var teacher = new Teacher { FirstName = "Иван", LastName = "Иванов", MiddleName = "" };
        teacher.ShortName.Should().Be(teacher.DisplayName);
    }

    [Fact]
    public void NewTeacher_Id_IsNotEmpty()
    {
        new Teacher().Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void TwoNewTeachers_HaveDifferentIds()
    {
        new Teacher().Id.Should().NotBe(new Teacher().Id);
    }
}
