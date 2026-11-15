using System;
using System.IO;
using System.Linq;
using System.Threading;

internal sealed class StartupFolderEntry {
    internal string Name, Path;
    internal bool Paused;
    internal string StartupRoot, PausedRoot;

    internal static StartupFolderEntry Active(string root, string path) {
        if (root == null || path == null || !String.Equals(System.IO.Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase)) return null;
        return new StartupFolderEntry { Name=System.IO.Path.GetFileName(path), Path=path, StartupRoot=root };
    }
    internal static StartupFolderEntry PausedEntry(string startup, string paused, string path) {
        if (startup == null || paused == null || path == null || !String.Equals(System.IO.Path.GetDirectoryName(path), paused, StringComparison.OrdinalIgnoreCase)) return null;
        return new StartupFolderEntry { Name=System.IO.Path.GetFileName(path), Path=path, Paused=true, StartupRoot=startup, PausedRoot=paused };
    }
    internal void Move() {
        if (String.IsNullOrEmpty(Name) || Name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Entrada da pasta de inicialização inválida.");
        using (var gate=new Mutex(false,@"Local\AdvancedSystemCraft.StartupFolderEdit")) {
            bool owned;
            try { owned=gate.WaitOne(0); } catch (AbandonedMutexException) { owned=true; }
            if (!owned) throw new IOException("Outra janela está alterando a inicialização.");
            try { Transfer(); } finally { gate.ReleaseMutex(); }
        }
    }
    void Transfer() {
        string source=Paused ? PausedRoot : StartupRoot;
        string destination=Paused ? StartupRoot : PausedRoot;
        if (String.IsNullOrEmpty(source) || String.IsNullOrEmpty(destination) ||
            !String.Equals(System.IO.Path.GetDirectoryName(Path), source, StringComparison.OrdinalIgnoreCase) || !File.Exists(Path))
            throw new IOException("O arquivo mudou. Atualize a lista.");
        Directory.CreateDirectory(destination);
        string target=System.IO.Path.Combine(destination,Name);
        if (File.Exists(target)) throw new IOException("Já existe um arquivo com esse nome no destino. Os dois arquivos foram preservados.");
        File.Move(Path,target);
    }
}
