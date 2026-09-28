namespace AntonsBackupManager.Infrastructure.Execution;

public interface IFileTransfer
{
    void Copy(string source, string destination, CancellationToken cancellationToken);
}
