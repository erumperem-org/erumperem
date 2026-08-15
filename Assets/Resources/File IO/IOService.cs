using System;
using System.IO;
using System.Threading.Tasks;

namespace Services.IO
{
    public struct FileData
    {
        public string _fileContent;
        public string _fileName;
        public string _filePath;

        public FileData(string fileContent, string fileName, string filePath)
        {
            _fileContent = fileContent;
            _fileName    = fileName;
            _filePath    = filePath;
        }

        /// <summary>Full path including file name (e.g. /tmp/logs/app.txt).</summary>
        public string FullPath => Path.Combine(_filePath, _fileName);
    }

    public interface IFileService
    {
        Task WriteAsync(FileData fileData);
        Task<FileData> ReadAsync(string fileName, string filePath);
        Task<bool> ExistsAsync(string fileName, string filePath);
        Task DeleteAsync(string fileName, string filePath);
    }

    public sealed class FileService : IFileService
    {
        // ── Write ─────────────────────────────────────────────────────────

        /// <summary>
        /// Grava <see cref="FileData._fileContent"/> em disco,
        /// criando os diretórios necessários se não existirem.
        /// </summary>
        public async Task WriteAsync(FileData fileData)
        {
            ValidateName(fileData._fileName, nameof(fileData._fileName));
            ValidatePath(fileData._filePath, nameof(fileData._filePath));

            Directory.CreateDirectory(fileData._filePath);
            await File.WriteAllTextAsync(fileData.FullPath, fileData._fileContent ?? string.Empty);
        }

        // ── Read ──────────────────────────────────────────────────────────

        /// <summary>
        /// Lê o arquivo em <paramref name="filePath"/>/<paramref name="fileName"/>
        /// e retorna um <see cref="FileData"/> populado.
        /// Lança <see cref="FileNotFoundException"/> se o arquivo não existir.
        /// </summary>
        public async Task<FileData> ReadAsync(string fileName, string filePath)
        {
            ValidateName(fileName, nameof(fileName));
            ValidatePath(filePath, nameof(filePath));

            string fullPath = Path.Combine(filePath, fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File not found: {fullPath}");

            string content = await File.ReadAllTextAsync(fullPath);
            return new FileData(content, fileName, filePath);
        }

        // ── Exists ────────────────────────────────────────────────────────

        /// <summary>Retorna <c>true</c> se o arquivo existir em disco.</summary>
        public Task<bool> ExistsAsync(string fileName, string filePath)
        {
            // Não lança para paths inválidos — retorna false de forma segura,
            // evitando que chamadores precisem tratar exceção para um simples "existe?".
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(filePath))
                return Task.FromResult(false);

            string fullPath = Path.Combine(filePath, fileName);
            return Task.FromResult(File.Exists(fullPath));
        }

        // ── Delete ────────────────────────────────────────────────────────

        /// <summary>
        /// Apaga o arquivo se existir; não faz nada caso contrário.
        /// Não lança exceção se o arquivo não existir.
        /// </summary>
        public Task DeleteAsync(string fileName, string filePath)
        {
            // Não lança para paths inválidos — um delete de arquivo inexistente
            // é considerado sucesso (idempotente).
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(filePath))
                return Task.CompletedTask;

            string fullPath = Path.Combine(filePath, fileName);

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.CompletedTask;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void ValidateName(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("File name cannot be null or empty.", paramName);
        }

        private static void ValidatePath(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("File path cannot be null or empty.", paramName);
        }
    }
}