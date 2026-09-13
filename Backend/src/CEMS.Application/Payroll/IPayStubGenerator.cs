namespace CEMS.Application.Payroll;

public interface IPayStubGenerator
{
    byte[] Generate(PayStubData data);
}
