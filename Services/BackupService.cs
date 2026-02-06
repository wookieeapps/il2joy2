namespace Il2Joy2.Services;

/// <summary>
/// Service for creating timestamped backups of files
/// </summary>
public sealed class BackupService
{
    /// <summary>
    /// Creates a backup of the specified file with a timestamp suffix.
    /// Format: filename.ext.backup_YYYYMMDD_HHmmss
    /// </summary>
    /// <param name="filePath">Path to the file to backup</param>
    /// <returns>Path to the created backup file</returns>
    public string CreateBackup(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Cannot backup non-existent file: {filePath}");
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = $"{filePath}.backup_{timestamp}";

        File.Copy(filePath, backupPath, overwrite: false);

        return backupPath;
    }

    /// <summary>
    /// Creates a backup only if it doesn't already exist for this timestamp.
    /// Returns null if backup was skipped.
    /// </summary>
    public string? CreateBackupIfNeeded(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = $"{filePath}.backup_{timestamp}";

        if (File.Exists(backupPath))
        {
            return null; // Backup already exists for this timestamp
        }

        File.Copy(filePath, backupPath, overwrite: false);
        return backupPath;
    }
}
