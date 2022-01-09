using Extended_Programmable_Block;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace TestScript {

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class Main : MySessionComponentBase {

        public static Main Instance;
        public Dictionary<long, String> pathString = new Dictionary<long, String>();
        private ServerCommunication serverCommunication;

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent) {
            //MyLog.Default.WriteLineAndConsole("Main.Init()");
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            MyAPIGateway.TerminalControls.CustomActionGetter += CustomActionGetter;
            serverCommunication = new ServerCommunication();
            if (ServerCommunication.IsClient) serverCommunication.onClientInit();
            MyLog.Default.WriteLineAndConsole(
                $"\n" +
                $"\nEPB: Init" +
                $"\n{(MyAPIGateway.Multiplayer != null ? "Multiplayer" : "Singleplayer")}" +
                $"\n{(ServerCommunication.IsServer ? "Is Server" : "Is Not Server")}" +
                $"\n{(ServerCommunication.IsClient ? "Is Client" : "Is Not Client")}" +
                $"\n");
        }

        public override void LoadData() {
            //MyLog.Default.WriteLineAndConsole("Main.LoadData()");
            Instance = this;
            if(ServerCommunication.IsClient) load(pathString);
        }

		protected override void UnloadData() {
            //MyLog.Default.WriteLineAndConsole("Main.UnloadData()");
            serverCommunication.Unload();

        }

		public override void SaveData() {
            //Log.Info("SAVED");
            //save(pathString);
        }

        public void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls) {
            if (block is IMyProgrammableBlock) {
                IMyTerminalControls TerminalControls = MyAPIGateway.TerminalControls;

                var path = TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyProgrammableBlock>("pbe_path");

                path.Title = MyStringId.GetOrCompute("Path");
                path.Tooltip = MyStringId.GetOrCompute("File Path");
                path.Visible = (g) => true;
                path.Enabled = (g) => true;
                path.Getter = (b) => {
                    String s = "";
                    pathString.TryGetValue(((IMyProgrammableBlock)b).EntityId, out s);
                    return new StringBuilder(s);
                };
                path.Setter = (b, builder) => {
                    long id = ((IMyProgrammableBlock)b).EntityId;
                    b.CustomData = id + "";
                    if (pathString.ContainsKey(id)) pathString.Remove(id);
                    pathString.Add(id, builder.ToString());

					if (ServerCommunication.IsClient && ServerCommunication.IsServer) {
                        save(pathString);
                    } else {
                        serverCommunication.onClientUpdateName(id, builder.ToString());
                    }
                };
                controls.Add(path);


                var loadButton = TerminalControls.CreateControl<IMyTerminalControlButton, IMyProgrammableBlock>("pbe_load");

                loadButton.Title = MyStringId.GetOrCompute("Load Data");
                loadButton.Tooltip = MyStringId.GetOrCompute("Load Some Data");
                loadButton.Visible = (g) => true;
                loadButton.Enabled = (g) => true;
                loadButton.Action = onClickLoad;
                controls.Add(loadButton);


                var saveButton = TerminalControls.CreateControl<IMyTerminalControlButton, IMyProgrammableBlock>("pbe_save");

                saveButton.Title = MyStringId.GetOrCompute("Save Data");
                saveButton.Tooltip = MyStringId.GetOrCompute("Save");
                saveButton.Visible = (g) => true;
                saveButton.Enabled = (g) => true;/*{
                    String PBPath = getProgrammableBlockPath(g);
                    return PBPath != null && PBPath.Length > 0;
                };*/
                saveButton.Action = onClickSave;
                controls.Add(saveButton);
            }
        }
        public void onClickSave(IMyTerminalBlock block) {
            if (MyAPIGateway.Multiplayer != null && !MyAPIGateway.Multiplayer.IsServer) {
                serverCommunication.onClientSave(block.EntityId);
                return;
            }

            //MyLog.Default.WriteLineAndConsole("EPB[DEBUG]: saving");
            IMyProgrammableBlock programmableBlock = block as IMyProgrammableBlock;
            //MyLog.Default.WriteLineAndConsole("EPB[DEBUG]: Storagedata: " + (programmableBlock.StorageData == null ? "null" : "not null"));
            String path = "";
            if (!pathString.TryGetValue(programmableBlock.EntityId, out path)) {
                MyLog.Default.WriteLineAndConsole("EPB[DEBUG]: Failed to Save, x01");
                return;
            }           

            // Fix because StorageData is null on failed PB
            bool stateBevore = programmableBlock.Enabled;
            if (stateBevore) programmableBlock.Enabled = false;
            programmableBlock.Recompile();

            //MyLog.Default.WriteLineAndConsole("EPB[DEBUG]: Storagedata_1: " + (programmableBlock.StorageData == null ? "null" : "not null"));

            try {
                BinaryWriter w = MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage(path, typeof(Main));
                char[] raw = programmableBlock.StorageData.ToCharArray();
                byte[] bin = new byte[raw.Length];
                for (int i = 0; i < raw.Length; i++) bin[i] = (byte)(raw[i] & 0xFF);
                //MyLog.Default.WriteLineAndConsole("EPB: wBS " + w.BaseStream.ToString());
                w.Write(bin, 0, bin.Length);
                w.Flush();
                w.Close();
            } catch (Exception ex) {
                MyLog.Default.WriteLineAndConsole($"\n\n\n\n\n\nEPB: Exception Save File: {ex.Message} -->> {ex.StackTrace}\n\n\n\n\n\n");
            }

            programmableBlock.Enabled = stateBevore;
        }
        public void onClickLoad(IMyTerminalBlock block) {
            if (MyAPIGateway.Multiplayer != null && !MyAPIGateway.Multiplayer.IsServer) {
                serverCommunication.onClientLoad(block.EntityId);
                return;
            }

            //MyLog.Default.WriteLineAndConsole("EPB[DEBUG]: loading");

            IMyProgrammableBlock programmableBlock = block as IMyProgrammableBlock;
            String path = "";
            if (!pathString.TryGetValue(programmableBlock.EntityId, out path)) {
                MyLog.Default.WriteLineAndConsole("EPB[DEBUG]: Failed to Load, x11");
                return;
            }
            //MyLog.Default.WriteLineAndConsole("EPB[DEBUG]: Loading " + path);
            BinaryReader r = null;
            try {
                r = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage(path, typeof(Main));
                //MyLog.Default.WriteLineAndConsole("EPB: rBS " + r.BaseStream.ToString());
            } catch (Exception ex) {
                MyLog.Default.WriteLineAndConsole($"\n\n\n\n\n\nEPB: Exception Load File: {ex.Message}\n\n\n\n\n\n");
                return;
            }
            List<byte[]> chunks = new List<byte[]>();
            int writePointer = 0;
            byte[] cache = new byte[4096];
            int l;
            while ((l = r.Read(cache, 0, cache.Length)) > 0) {
                byte[] entry = new byte[l];
                Array.Copy(cache, 0, entry, 0, l);
                chunks.Add(entry);
                writePointer += l;
            }
            int pos = 0;
            char[] b = new char[writePointer];
            foreach (byte[] entry in chunks) {
                for (int i = 0; i < entry.Length; i++) b[i + pos] = (char)(entry[i] & 0xFF);
                pos += entry.Length;
            }

            // Fix because StorageData is null on failed PB
            bool stateBevore = programmableBlock.Enabled;
            if (stateBevore) programmableBlock.Enabled = false;
            programmableBlock.Recompile();
            programmableBlock.Enabled = stateBevore;

            programmableBlock.StorageData = new String(b);
        }
        public void CustomActionGetter(IMyTerminalBlock block, List<IMyTerminalAction> actions) {

            //Log.Info(block.GetType() + " - " + String.Join(",", actions));
            if (block is IMyProgrammableBlock) {
                //Log.Info(String.Join(",", actions));
            }
        }
        public String getProgrammableBlockPath(IMyTerminalBlock block) {
            long id = ((IMyProgrammableBlock)block).EntityId;
            String ret = null;
            if (!pathString.TryGetValue(id, out ret)) return null;
            return ret;
        }

        #region PathData
        public void load(Dictionary<long, String> a) {
            a.Clear();
            try {
                BinaryReader r = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage("settings.dat", typeof(Main));
                int c = readInt(r);
                for (int i = 0; i < c; i++) {
                    long k = long.Parse(readString(r, false));//readLong(r);
                    String s = readString(r, false);
                    a.Add(k, s);
                }
                r.Close();
            } catch (Exception ex) {
                //Log.Info("Failed to load settings.dat");
            }
        }

        public void save(Dictionary<long, String> map) {
            BinaryWriter w = MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage("settings.dat", typeof(Main));
            writeInt(w, map.Count);
            foreach (long k in map.Keys) {
                //writeLong(w, k);

                writeString(w, k.ToString(), false);
                writeString(w, map[k], false);
            }
            w.Flush();
            w.Close();
        }

        public String readString(BinaryReader r, bool fullChar) {
            int c = readInt(r);
            char[] a = new char[c];
            char chr;
            for (int i = 0; i < c; i++) {
                chr = (char)0;
                if (fullChar) chr |= (char)((r.Read()) << 8);
                chr |= (char)r.Read();
                a[i] = chr;
            }

            return new String(a);
        }

        public void writeString(BinaryWriter w, String s, bool fullChar) {
            char[] str = s.ToCharArray();
            writeInt(w, str.Length);
            for (int i = 0; i < str.Length; i++) {
                if (fullChar) w.Write((byte)((str[i] >> 8) & 0xFF));
                w.Write((byte)(str[i] & 0xFF));
            }
        }
        public int readInt(BinaryReader r) {
            char[] raw = new char[4];
            r.Read(raw, 0, raw.Length);
            return System.BitConverter.ToInt32(new byte[] { (byte)raw[3], (byte)raw[2], (byte)raw[1], (byte)raw[0] }, 0);
        }

        public void writeInt(BinaryWriter w, int i) {
            w.Write(new byte[]{
            (byte)((i >> 24) & 0xFF),
            (byte)((i >> 16) & 0xFF),
            (byte)((i >> 8) & 0xFF),
            (byte)((i) & 0xFF)
        });
        }
        /*
        public int readLong(BinaryReader r) {
            char[] raw = new char[8];
            r.Read(raw, 0, raw.Length);
            return System.BitConverter.ToInt32(new byte[] { (byte)raw[7], (byte)raw[6], (byte)raw[5], (byte)raw[4], (byte)raw[3], (byte)raw[2], (byte)raw[1], (byte)raw[0] }, 0);
        }

        public void writeLong(BinaryWriter w, long i) {
            w.Write(new byte[]{
            (byte)((i) & 0xFF),
            (byte)((i >> 8) & 0xFF),
            (byte)((i >> 16) & 0xFF),
            (byte)((i >> 24) & 0xFF),
            (byte)((i >> 32) & 0xFF),
            (byte)((i >> 40) & 0xFF),
            (byte)((i >> 48) & 0xFF),
            (byte)((i >> 56) & 0xFF)
        });
        }
        */
        #endregion        
    }
}
