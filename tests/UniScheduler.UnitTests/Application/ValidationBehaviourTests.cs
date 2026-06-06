using FluentAssertions;
using FluentValidation;
using MediatR;
using UniScheduler.Application.Common.Behaviours;
using Xunit;
using ValidationException = UniScheduler.Application.Common.Exceptions.ValidationException;

namespace UniScheduler.UnitTests.Application;

// Command must be public for FluentValidation's generic interface to be proxied
public record TestValidationCommand(string Value) : IRequest<string>;

public class PassingTestValidator : AbstractValidator<TestValidationCommand>
{
    // No rules - always passes
}

public class FailingTestValidator : AbstractValidator<TestValidationCommand>
{
    public FailingTestValidator()
    {
        RuleFor(x => x.Value).NotEmpty().WithMessage("Value is required.");
    }
}

public class SecondFailingValidator : AbstractValidator<TestValidationCommand>
{
    public SecondFailingValidator()
    {
        RuleFor(x => x.Value).MinimumLength(10).WithMessage("Too short.");
    }
}

public class ValidationBehaviourTests
{
    // MediatR 12: RequestHandlerDelegate<T> = Func<CancellationToken, Task<T>>
    private static RequestHandlerDelegate<string> OkDelegate(out bool flag)
    {
        var called = false;
        flag = called;
        RequestHandlerDelegate<string> d = _ => { called = true; return Task.FromResult("ok"); };
        return d;
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behaviour = new ValidationBehaviour<TestValidationCommand, string>([]);
        var wasCalled = false;
        RequestHandlerDelegate<string> next = _ => { wasCalled = true; return Task.FromResult("ok"); };

        var result = await behaviour.Handle(new TestValidationCommand("x"), next, CancellationToken.None);

        wasCalled.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_PassingValidator_CallsNext()
    {
        var behaviour = new ValidationBehaviour<TestValidationCommand, string>([new PassingTestValidator()]);
        var wasCalled = false;
        RequestHandlerDelegate<string> next = _ => { wasCalled = true; return Task.FromResult("ok"); };

        await behaviour.Handle(new TestValidationCommand("x"), next, CancellationToken.None);

        wasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FailingValidator_ThrowsValidationException()
    {
        var behaviour = new ValidationBehaviour<TestValidationCommand, string>([new FailingTestValidator()]);
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        var act = async () => await behaviour.Handle(new TestValidationCommand(""), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_FailingValidator_DoesNotCallNext()
    {
        var behaviour = new ValidationBehaviour<TestValidationCommand, string>([new FailingTestValidator()]);
        var wasCalled = false;
        RequestHandlerDelegate<string> next = _ => { wasCalled = true; return Task.FromResult("ok"); };

        try { await behaviour.Handle(new TestValidationCommand(""), next, CancellationToken.None); }
        catch (ValidationException) { }

        wasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MultipleFailingValidators_ThrowsWithAllErrors()
    {
        var behaviour = new ValidationBehaviour<TestValidationCommand, string>(
            [new FailingTestValidator(), new SecondFailingValidator()]);
        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => behaviour.Handle(new TestValidationCommand(""), next, CancellationToken.None));

        ex.Errors.Values.SelectMany(v => v).Should().HaveCountGreaterOrEqualTo(1);
    }
}
