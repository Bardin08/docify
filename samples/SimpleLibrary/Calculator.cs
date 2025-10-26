namespace SimpleLibrary;

/// <summary>
/// Provides basic arithmetic operations for mathematical calculations.
/// </summary>
public class Calculator
{
    /// <summary>
    /// Initializes a new instance of the Calculator class with default precision.
    /// </summary>
    public Calculator()
    {
        Precision = 2;
    }

    public Calculator(int precision)
    {
        Precision = precision;
    }

    /// <summary>
    /// Gets or sets the decimal precision for calculation results.
    /// </summary>
    public int Precision { get; set; }

    public string LastOperation { get; private set; } = string.Empty;

    /// <summary>
    /// Adds two numbers and returns the result.
    /// </summary>
    /// <param name="a">The first number to add.</param>
    /// <returns>The sum of the two numbers.</returns>
    public double Add(double a, double b)
    {
        LastOperation = $"{a} + {b}";
        return Math.Round(a + b, Precision);
    }

    public double Subtract(double a, double b)
    {
        LastOperation = $"{a} - {b}";
        return Math.Round(a - b, Precision);
    }

    public double Multiply(double a, double b)
    {
        LastOperation = $"{a} * {b}";
        return Math.Round(a * b, Precision);
    }

    public double Divide(double a, double b)
    {
        if (b == 0)
        {
            throw new DivideByZeroException("Cannot divide by zero.");
        }
        LastOperation = $"{a} / {b}";
        return Math.Round(a / b, Precision);
    }

    public double Power(double baseNumber, double exponent)
    {
        LastOperation = $"{baseNumber} ^ {exponent}";
        return Math.Round(Math.Pow(baseNumber, exponent), Precision);
    }

    public void ClearHistory()
    {
        LastOperation = string.Empty;
    }
}
