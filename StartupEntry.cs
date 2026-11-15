using System;
using System.IO;
using System.Threading;
using Microsoft.Win32;

internal sealed class StartupEntry {
    internal const string RunPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string BackupPath=@"Software\AdvancedSystemCraft\PausedStartup";
    internal string Name,Command;
    internal RegistryValueKind Kind;
    internal bool Paused;

    internal static StartupEntry Read(RegistryKey key,string name,bool paused) {
        if(key==null || String.IsNullOrEmpty(name))return null;
        var raw=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        if(String.IsNullOrEmpty(raw))return null;
        var kind=key.GetValueKind(name);
        if(kind!=RegistryValueKind.String && kind!=RegistryValueKind.ExpandString)return null;
        return new StartupEntry{Name=name,Command=raw,Kind=kind,Paused=paused};
    }

    internal void Move(RegistryKey root,string runPath,string backupPath) {
        using(var gate=new Mutex(false,@"Local\AdvancedSystemCraft.StartupEdit")){
            bool owned;
            try{owned=gate.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
            if(!owned)throw new IOException("Outra janela está alterando a inicialização.");
            try{MoveValue(root,runPath,backupPath);}finally{gate.ReleaseMutex();}
        }
    }
    void MoveValue(RegistryKey root,string runPath,string backupPath) {
        using(var source=root.OpenSubKey(Paused?backupPath:runPath,true)){
            var current=Read(source,Name,Paused);
            if(current==null || current.Command!=Command || current.Kind!=Kind)
                throw new IOException("A entrada mudou. Atualize a lista.");
            using(var destination=root.CreateSubKey(Paused?runPath:backupPath)){
                if(Array.Exists(destination.GetValueNames(),n=>String.Equals(n,Name,StringComparison.OrdinalIgnoreCase))){
                    var existing=Read(destination,Name,!Paused);
                    if(existing==null || existing.Kind!=Kind || existing.Command!=Command)
                        throw new IOException("Já existe outra entrada com esse nome. Os dois valores foram preservados.");
                }else{
                    destination.SetValue(Name,Command,Kind);
                    destination.Flush();
                }
                var latest=Read(source,Name,Paused);
                if(latest==null || latest.Kind!=Kind || latest.Command!=Command)
                    throw new IOException("A origem mudou durante a operação. A cópia foi preservada.");
                source.DeleteValue(Name);
            }
        }
    }
}
