namespace StockReportGenerator;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            ParsedStockReportGeneratorArguments invocation = StockReportGeneratorArguments.Parse(args);
            StockReportWorkbookGenerator.WriteFile(invocation.OutputPath, invocation.Options);
            Console.WriteLine($"Wrote {invocation.Options.RowCount} stock rows to {invocation.OutputPath}.");
            return 0;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(
                "Usage: StockReportGenerator --rows <count> --output <file.xlsx> [--seed <int>] [--headers en|uk] [--sheet <name>]");
            return 1;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
