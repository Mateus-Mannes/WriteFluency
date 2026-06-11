using WriteFluency.ExerciseTransfer;

try
{
    var options = TransferOptions.Parse(args);
    if (options.ShowHelp)
    {
        TransferOptions.PrintHelp();
        return 0;
    }

    using var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

    var transfer = new ExerciseTransferService(options);
    await transfer.RunAsync(cancellationSource.Token);
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Exercise transfer cancelled.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Exercise transfer failed: {exception.Message}");
    return 1;
}
