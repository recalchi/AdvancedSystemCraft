using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

internal sealed class IgnoredPaths {
    const string Header = "Advanced System Craft - ignored paths v1";
    static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    readonly string directory;
    readonly string file;

    internal IgnoredPaths(string directory) {
        this.directory = Path.GetFullPath(directory);
        file = Path.Combine(this.directory, "ignored-paths.txt");
    }

    internal static IgnoredPaths ForUser() {
        return new IgnoredPaths(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AdvancedSystemCraft"));
    }

    static string ExactPath(string value) {
        if (String.IsNullOrEmpty(value) || value.IndexOfAny(new[]{'\r','\n','\0'}) >= 0 ||
            !(value.StartsWith(@"\\",StringComparison.Ordinal) ||
              (value.Length > 2 && Char.IsLetter(value[0]) && value[1] == ':' &&
               (value[2] == '\\' || value[2] == '/'))))
            throw new ArgumentException("A exclusão exige o caminho absoluto do arquivo.");
        return Path.GetFullPath(value);
    }

    internal HashSet<string> Load() {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        try {
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            using (var reader = new StreamReader(stream, Utf8, false)) {
                if (reader.ReadLine() != Header)
                    throw new InvalidDataException("Formato desconhecido na lista de exclusões.");
                string value;
                while ((value = reader.ReadLine()) != null) paths.Add(ExactPath(value));
            }
        } catch (FileNotFoundException) {} catch (DirectoryNotFoundException) {}
        return paths;
    }

    internal HashSet<string> Ignore(IEnumerable<string> paths) { return Change(paths, true); }
    internal HashSet<string> Remove(IEnumerable<string> paths) { return Change(paths, false); }

    HashSet<string> Change(IEnumerable<string> paths, bool add) {
        string[] requested = paths.Select(ExactPath).Distinct(StringComparer.Ordinal).ToArray();
        if (requested.Length == 0) return Load();
        Directory.CreateDirectory(directory);
        using (var edit = new FileStream(file + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)) {
            var current = Load();
            bool changed = false;
            foreach (string path in requested) changed |= add ? current.Add(path) : current.Remove(path);
            if (!changed) return current;
            var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(output, Utf8)) {
                    writer.WriteLine(Header);
                    foreach (string path in current.OrderBy(p=>p, StringComparer.Ordinal)) writer.WriteLine(path);
                    writer.Flush();
                    output.Flush(true);
                }
                if (File.Exists(file)) File.Replace(temporary, file, null);
                else File.Move(temporary, file);
            } finally { File.Delete(temporary); }
            return current;
        }
    }
}
