namespace SimpleLibrary;

public class CalculatorUsage
{
    private readonly Calculator _calculator;

    public CalculatorUsage()
    {
        _calculator = new Calculator();
    }

    public double CalculateTotal(double price, double tax)
    {
        var taxAmount = _calculator.Multiply(price, tax);
        return _calculator.Add(price, taxAmount);
    }

    public double CalculateDiscount(double price, double discountPercent)
    {
        var discount = _calculator.Multiply(price, discountPercent);
        return _calculator.Subtract(price, discount);
    }

    public double CalculateCompoundInterest(double principal, double rate, double years)
    {
        return _calculator.Power(1 + rate, years);
    }
}
