namespace VirtualController.Infrastructure.Persistence;

/// <summary>
/// Escritura atómica (ADR-004, punto 8). Escribir directamente sobre el destino puede dejar un JSON
/// truncado si el proceso muere a mitad, destruyendo el perfil anterior; aquí el destino solo se
/// reemplaza cuando el contenido nuevo está completo en disco.
/// </summary>
public static class AtomicFileWriter
{
    public static void Write(string destinationPath, string content)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".tmp";

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(content);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(destinationPath))
        {
            var backupPath = destinationPath + ".bak";
            File.Replace(tempPath, destinationPath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, destinationPath);
        }
    }
}
