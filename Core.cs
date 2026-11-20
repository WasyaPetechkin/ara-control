using System;
using System.Reflection;
using System.IO;
using System.Linq;

using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32.SafeHandles;

[assembly: AssemblyTitle("Ara Control")]
[assembly: AssemblyProduct("Ara Control")]
[assembly: AssemblyDescription("Windows controls for the AKG Ara USB microphone")]
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]

public class Profile {
 public string Name, Process, Button, Up, Down;
 public bool Wheel, FocusOnly;
 public override string ToString(){return Name;}
}
public class Settings {
 public List<Profile> Profiles = new List<Profile>();
 public int Selected;
 public string Language = "system";
 public string Theme = "system";
 public bool Enabled;
 public string ClockwiseCode;
}
class Decoder {
    private bool expectingWheelRelease;
    private bool buttonDown;

    public string Read(byte[] data, int size) {
        if (size != 16 || data[0] != 1)
            return null;

        int code = data[1];
        if (code != 0) {
            buttonDown = false;
            expectingWheelRelease = true;
            if (code == 2) return "up";
            if (code == 32) return "down";
            return null;
        }

        // A wheel step ends with a neutral report. The push button produces
        // two neutral reports of its own, one on press and one on release.
        if (expectingWheelRelease) {
            expectingWheelRelease = false;
            return null;
        }
        buttonDown = !buttonDown;
        return buttonDown ? null : "button";
    }
}

static class Device {
    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_List_SizeW(out uint size, ref Guid guid, string id, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_Interface_ListW(ref Guid guid, string id, [Out] char[] buffer, uint size, uint flags);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr sa, uint mode, uint flags, IntPtr template);

    [DllImport("kernel32")]
    private static extern bool ReadFile(SafeFileHandle handle, byte[] bytes, int capacity, out int count, IntPtr overlapped);

    private static string FindAra(Guid hidClass) {
        uint size;
        if (CM_Get_Device_Interface_List_SizeW(out size, ref hidClass, null, 0) != 0)
            return null;

        char[] paths = new char[size];
        if (CM_Get_Device_Interface_ListW(ref hidClass, null, paths, size, 0) != 0)
            return null;

        return new string(paths).Split('\0').FirstOrDefault(path =>
            path.IndexOf("vid_0ecb&pid_2057&mi_03", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    public static void Loop(Action<bool> status, Action<string> eventReceived) {
        Guid hidClass;
        HidD_GetHidGuid(out hidClass);
        bool? connected = null;
        for (;;) {
            string path = FindAra(hidClass);
            if (path != null) {
                using (var device = CreateFile(path, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
                    if (!device.IsInvalid) {
                        status(true);
                        connected = true;
                        var decoder = new Decoder();
                        byte[] report = new byte[256];
                        int size;
                        while (ReadFile(device, report, report.Length, out size, IntPtr.Zero) && size > 0) {
                            string action = decoder.Read(report, size);
                            if (action != null) eventReceived(action);
                        }
                    }
                }
            }
            if (connected != false) status(false);
            connected = false;
            Thread.Sleep(1800);
        }
    }
}
static class Shortcuts {
 [StructLayout(LayoutKind.Sequential)] struct Input {public uint type;public Union value;}
 [StructLayout(LayoutKind.Explicit)] struct Union {[FieldOffset(0)] public Keyboard key;[FieldOffset(0)] public Mouse mouse;}
 [StructLayout(LayoutKind.Sequential)] struct Keyboard {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct Mouse {public int x,y;public uint data,flags,time;public UIntPtr extra;}
 [DllImport("user32")] static extern uint SendInput(uint count,Input[] inputs,int size);
 public static List<Keys> Parse(string chord){
  var keys=new List<Keys>();
  foreach(string part in chord.Split('+')){
   string k=part.Trim();Keys key;
   if(k.Equals("Ctrl",StringComparison.OrdinalIgnoreCase))key=Keys.ControlKey;
   else if(k.Equals("Shift",StringComparison.OrdinalIgnoreCase))key=Keys.ShiftKey;
   else if(k.Equals("Alt",StringComparison.OrdinalIgnoreCase))key=Keys.Menu;
   else if(!Enum.TryParse<Keys>(k,true,out key)||!Enum.IsDefined(typeof(Keys),key)||(int)key<=0||(int)key>254)throw new Exception("Неизвестная клавиша: "+k);
   if(keys.Contains(key))throw new Exception("Клавиша повторяется: "+k);
   keys.Add(key);
  }
  if(keys.All(k=>k==Keys.ControlKey||k==Keys.ShiftKey||k==Keys.Menu))throw new Exception("Добавьте основную клавишу.");
  return keys;
 }
 [DllImport("user32")] static extern uint MapVirtualKey(uint key,uint mode);
 private static Input KeyboardEvent(Keys key, bool release) {
     uint scan = MapVirtualKey((uint)key, 4);
     Input input = new Input();
     input.type = 1;
     input.value.key.vk = (ushort)key;
     input.value.key.scan = (ushort)(scan & 255);
     input.value.key.flags = (scan == 0 ? 0u : 8u) | (scan > 255 ? 1u : 0u) | (release ? 2u : 0u);
     return input;
 }
 public static bool Send(string chord) {
     var keys = Parse(chord);
     var down = keys.Select(key => KeyboardEvent(key, false)).ToArray();
     int itemSize = Marshal.SizeOf(typeof(Input));
     int pressed = (int)SendInput((uint)down.Length, down, itemSize);
     if (pressed == 0) return false;

     // Hold the chord long enough for the Discord desktop client's global binding.
     Thread.Sleep(90);
     var up = keys.Take(pressed).Reverse().Select(key => KeyboardEvent(key, true)).ToArray();
     int released = (int)SendInput((uint)up.Length, up, itemSize);
     if (released < up.Length) {
         var remaining = up.Skip(released).ToArray();
         released += (int)SendInput((uint)remaining.Length, remaining, itemSize);
     }
     return pressed == down.Length && released == pressed;
 }
 public static string Format(Keys keyData) {
  Keys code = keyData & Keys.KeyCode;
  if(code == Keys.None || code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu
     || code == Keys.LWin || code == Keys.RWin)
   return null;
  var parts = new List<string>();
  if((keyData & Keys.Control) != 0) parts.Add("Ctrl");
  if((keyData & Keys.Shift) != 0) parts.Add("Shift");
  if((keyData & Keys.Alt) != 0) parts.Add("Alt");
  parts.Add(code.ToString()); return string.Join("+",parts);
 }
}

static class SettingsStore {
 static string Folder {
  get {
   return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AraControl");
  }
 }
 public static string DefaultButton(string process) {
  return string.Equals(process, "Discord", StringComparison.OrdinalIgnoreCase) ? "Ctrl+Shift+F9" : "";
 }
 public static Profile NewProfile(string name,string process) {
  return new Profile {
   Name=name, Process=process, Button=DefaultButton(process),
   Up="", Down=""
  };
 }
 public static Settings Load() {
  return Load(Path.Combine(Folder,"profiles.xml"));
 }
 internal static Settings Load(string file) {
  var result=new Settings();
  if(File.Exists(file)){
   using(var stream=File.OpenRead(file))
    result=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
  }
  if(result.Profiles==null) result.Profiles=new List<Profile>();
  result.Profiles.RemoveAll(p=>p==null || string.IsNullOrWhiteSpace(p.Name));
  if(result.Profiles.Count==0){
   result.Profiles.Add(NewProfile("Discord","Discord"));
   result.Profiles.Add(NewProfile("Telegram","Telegram"));
  }
  result.Selected=Math.Max(0,Math.Min(result.Selected,result.Profiles.Count-1));
  return result;
 }
 public static void Save(Settings settings) {
  Save(settings,Path.Combine(Folder,"profiles.xml"));
 }
 internal static void Save(Settings settings,string file) {
  string directory=Path.GetDirectoryName(file);
  Directory.CreateDirectory(directory);
  string temp=Path.Combine(directory,Guid.NewGuid().ToString()+".tmp");
  try {
   using(var stream=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(stream,settings);
   if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
  } finally { if(File.Exists(temp))File.Delete(temp); }
 }
}

static class Program {
 [STAThread]
 static void Main() {
  bool first;
  using(var mutex=new Mutex(true,"Local\\AraControlAKG",out first)){
   if(!first)return;
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   try {Application.Run(new MainWindow());}
   catch(Exception ex){MessageBox.Show(ex.Message,"Ara Control",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}





