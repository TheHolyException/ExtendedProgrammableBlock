using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TestScript;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Extended_Programmable_Block {
	public class ServerCommunication {//based on https://github.com/Blue64/Space_Engineers/blob/c6f659bbf20bcf6cc2e87bf9d677a13029f4bc75/AppData/Local/Temp/SpaceEngineers/655922051.sbm_NaniteConstructionSystem/NaniteConstructionSync.cs
		private const ushort comPortOffset = 8087;
		private const ushort toClientPort = comPortOffset+0;
		private const ushort toServerPort = comPortOffset+1;
		
        private bool isReady = false;
        
        public ServerCommunication() {
            isReady = false;
			Initialize();

		}
		//data.SteamId = MyAPIGateway.Session.Player.SteamUserId;
		public static bool IsServer {
			get {
				if (MyAPIGateway.Session == null)
					return false;

				if (MyAPIGateway.Multiplayer == null)
					return false;

				if (MyAPIGateway.Session.OnlineMode == VRage.Game.MyOnlineModeEnum.OFFLINE || MyAPIGateway.Multiplayer.IsServer)
					return true;

				return false;
			}
		}

		public static bool IsClient {
			get {
				if (MyAPIGateway.Session == null)
					return false;

				if (MyAPIGateway.Session.OnlineMode == VRage.Game.MyOnlineModeEnum.OFFLINE)
					return true;

				if (MyAPIGateway.Session.Player != null && MyAPIGateway.Session.Player.Client != null && MyAPIGateway.Multiplayer.IsServerPlayer(MyAPIGateway.Session.Player.Client))
					return true;

				if (!MyAPIGateway.Multiplayer.IsServer)
					return true;

				return false;
			}
		}


		public void Initialize() {
            if (!IsServer) { // Client, why do I have to be so difficult
                MyAPIGateway.Multiplayer.RegisterMessageHandler(toClientPort, onClientReceivePacket);
            } else {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(toServerPort, onServerReceivePacket);
            }
            isReady = true;
        }

        public void Unload() {
			if (isReady) {
                if (!IsServer) {
					MyAPIGateway.Multiplayer.UnregisterMessageHandler(toClientPort, onClientReceivePacket);
				} else {
					MyAPIGateway.Multiplayer.UnregisterMessageHandler(toServerPort, onServerReceivePacket);
				}
            }
        }
		//MyAPIGateway.Multiplayer.SendMessageToServer(8955, ASCIIEncoding.ASCII.GetBytes(MyAPIGateway.Utilities.SerializeToXML(new LoginData())));
		//LoginData loginData = MyAPIGateway.Utilities.SerializeFromXML<LoginData>(ASCIIEncoding.ASCII.GetString(data));
		public void clientSendPacket(byte[] data) {
			if(IsServer) return;
			try {
				MyAPIGateway.Multiplayer.SendMessageToServer(toServerPort, data);
            } catch (Exception ex) {
				//MyLog.Default.WriteLine(string.Format("clientSendPacket() Error: {0}", ex.ToString()));
			}
		}
		
		public void serverSendPacket(byte[] data) {
			if(!IsServer) return;
			try {
				List<IMyPlayer> players = new List<IMyPlayer>();
				MyAPIGateway.Players.GetPlayers(players);
				foreach (var player in players) {
					MyAPIGateway.Multiplayer.SendMessageTo(toClientPort, data, player.SteamUserId);
				}
            } catch (Exception ex) {
				//MyLog.Default.WriteLine(string.Format("serverSendPacket() Error: {0}", ex.ToString()));
			}
		}
		
        //s -> c: [Dict names, Dict filename update]

		private void onClientReceivePacket(byte[] data) {
			if(IsServer) return;
			try {
                BufferedReader r = new BufferedReader(data);
				int mode = r.read();
				switch(mode){
					case 1: onClientReceiveDict(Main.Instance.pathString, r); return;
					case 2: onClientUpdateDictEntry(Main.Instance.pathString, r); return;
					default: throw new Exception("undefined operation-ID " + mode);
				}
            } catch (Exception ex) {
                MyLog.Default.WriteLine(string.Format("onClientReceivePacket() Error: {0}", ex.ToString()));
            }
		}
		
		//c -> s: [request Dict, set filename, save, load]
		
		private void onServerReceivePacket(byte[] data) {
			if(!IsServer) return;
			try {
                BufferedReader r = new BufferedReader(data);
				int mode = r.read();
				switch(mode){
					case 1: onServerSendDict(Main.Instance.pathString); return;
					case 2: onServerUpdateDictEntry(Main.Instance.pathString, r); return;
					case 3: onServerSave(Main.Instance.pathString, r); return;
					case 4: onServerLoad(Main.Instance.pathString, r); return;
					default: throw new Exception("undefined operation-ID " + mode);
				}
            } catch (Exception ex) {
                MyLog.Default.WriteLine(string.Format("onServerReceivePacket() Error: {0}", ex.ToString()));
            }
		}
		//------------------ <interface> ------------------
		public void onClientInit(){
			BufferedWriter w = new BufferedWriter();
			w.write(1);
			clientSendPacket(w.toByteArray());
		}
		public void onClientUpdateName(long id, String newName) {
			//MyLog.Default.WriteLineAndConsole("onClientUpdateName():id="+id+", name="+ newName);
			BufferedWriter w = new BufferedWriter();
			w.write(2);
			w.writeString(id.ToString(), false);
			w.writeString(newName, false);
			clientSendPacket(w.toByteArray());
			//MyLog.Default.WriteLineAndConsole("onClientUpdateName():end");
		}
		public void onClientSave(long id){
			//MyLog.Default.WriteLineAndConsole("onClientSave()");
			BufferedWriter w = new BufferedWriter();
			w.write(3);
			w.writeString(id.ToString(), false);
			clientSendPacket(w.toByteArray());
			//MyLog.Default.WriteLineAndConsole("onClientSave():end");
		}
		public void onClientLoad(long id) {
			//MyLog.Default.WriteLineAndConsole("onClientLoad()");
			BufferedWriter w = new BufferedWriter();
			w.write(4);
			w.writeString(id.ToString(), false);
			clientSendPacket(w.toByteArray());
			//MyLog.Default.WriteLineAndConsole("onClientLoad():end");
		}

		private void onServerSave(Dictionary<long, String> map, BufferedReader r) {
			//MyLog.Default.WriteLineAndConsole("onServerSave()");
			long blockId = long.Parse(r.readString(false));
			String fileName = map[blockId];
			//MyLog.Default.WriteLineAndConsole($"onServerSave(): {blockId} - {fileName}");
			Main.Instance.onClickSave(getBlockById(blockId));
			//MyLog.Default.WriteLineAndConsole("onServerSave():end");
		}
		private void onServerLoad(Dictionary<long, String> map, BufferedReader r) {
			//MyLog.Default.WriteLineAndConsole("onServerLoad()");
			long blockId = long.Parse(r.readString(false));
			String fileName = map[blockId];
			Main.Instance.onClickLoad(getBlockById(blockId));
			//MyLog.Default.WriteLineAndConsole("onServerLoad():end");
		}
		//------------------ </interface> ------------------
		private void onClientReceiveDict(Dictionary<long, String> map, BufferedReader r) {
			readDict(map, r);
		}
		private void onClientUpdateDictEntry(Dictionary<long, String> map, BufferedReader r) {
			//MyLog.Default.WriteLineAndConsole("onClientUpdateDictEntry()");
			String idStr = r.readString(false);
			String name = r.readString(false);
			//MyLog.Default.WriteLineAndConsole("onClientUpdateDictEntry():id=" + idStr + ", name=" + name);
			long id = long.Parse(idStr);
			if(map.ContainsKey(id)) map.Remove(id);
			map.Add(id, name);
			Main.Instance.save(map);
			//MyLog.Default.WriteLineAndConsole("onClientUpdateDictEntry():end");
		}


		private void onServerSendDict(Dictionary<long, String> map){
			BufferedWriter w = new BufferedWriter();
			w.write(1);
			writeDict(map, w);
			serverSendPacket(w.toByteArray());
		}
		private void onServerUpdateDictEntry(Dictionary<long, String> map, BufferedReader r) {
			//MyLog.Default.WriteLineAndConsole("onServerUpdateDictEntry()");
			String idStr = r.readString(false);
			String name = r.readString(false);
			//MyLog.Default.WriteLineAndConsole("onServerUpdateDictEntry():id=" + idStr + ", name=" + name);
			long id = long.Parse(idStr);
			if(map.ContainsKey(id)) map.Remove(id);
			map.Add(id, name);
			Main.Instance.save(map);
			BufferedWriter w = new BufferedWriter();
			w.write(2);
			w.writeString(idStr, false);
			w.writeString(name, false);
			serverSendPacket(w.toByteArray());
			//MyLog.Default.WriteLineAndConsole("onServerUpdateDictEntry():end");
		}

		public void readDict(Dictionary<long, String> map, BufferedReader r) {
			Dictionary<long, String> a = new Dictionary<long, String>();
            try {
                int c = r.readInt();
                for (int i = 0; i < c; i++) {
                    long k = long.Parse(r.readString(false));//readLong(r);
                    String s = r.readString(false);
                    a.Add(k, s);
                }
				map.Clear();
				foreach (KeyValuePair<long, String> values in a) map.Add(values.Key, values.Value);
				
				
            } catch (Exception ex) {
                //Log.Info("Failed to load settings.dat");
            }
        }
		
		private void writeDict(Dictionary<long, String> map, BufferedWriter w){
			w.writeInt(map.Count);
            foreach (long k in map.Keys) {
                w.writeString(k.ToString(), false);
                w.writeString(map[k], false);
            }
		}

		private IMyTerminalBlock getBlockById(long blockid) {
			return MyAPIGateway.Entities.GetEntityById(blockid) as IMyTerminalBlock;
		}


	}
	
	public class BufferedWriter{
		private List<byte[]> data = new List<byte[]>();
		private int writePointer = 0;
		
		public BufferedWriter(){}
		public void write(int b){
			data.Add(new byte[]{(byte)b});
			writePointer++;
		}
		public void write(byte[] a){
			write(a, 0, a.Length);
		}
		
		public void write(byte[] a, int offset, int len){
			byte[] b = new byte[len];
			Array.Copy(a, offset, b, offset, len);
			data.Add(b);
			writePointer += len;
		}
		
		public void writeInt(int i) {
			write(new byte[]{
				(byte)((i >> 24) & 0xFF),
				(byte)((i >> 16) & 0xFF),
				(byte)((i >> 8) & 0xFF),
				(byte)((i) & 0xFF)
			}, 0, 4);
		}
		public void writeString(String s, bool fullChar) {
			char[] str = s.ToCharArray();
			//MyLog.Default.WriteLineAndConsole("writeString():len="+str.Length);
			writeInt(str.Length);
			if (fullChar) {
				byte[] a = new byte[str.Length*2];
				for (int i = 0; i < str.Length; i++) {
					a[i*2] = (byte)((str[i] >> 8) & 0xFF);
					a[i*2+1] = (byte)((str[i] & 0xFF));
				}
				write(a);
			} else {
				byte[] a = new byte[str.Length];
				for (int i = 0; i < str.Length; i++) {
					a[i] = (byte)((str[i] & 0xFF));
				}
				write(a);
			}
			//MyLog.Default.WriteLineAndConsole("writeString():end");
		}

		public byte[] toByteArray(){
			byte[] b = new byte[writePointer];
			int pos = 0;
			foreach (byte[] entry in data) {
				Array.Copy(entry, 0, b, pos, entry.Length);
				pos += entry.Length;
			}
			return b;
		}
	}

	public class BufferedReader{
		private byte[] data;
		private int readPos = 0;
		
		public BufferedReader(byte[] data){
			this.data = data;
		}
		
		public int read(){
			if(readPos >= data.Length) return -1;
			int a = data[readPos] & 0xFF;
			readPos++;
			return a;
		}
		
		public int read(byte[] r){
			return read(r, 0, r.Length);
		}
		
		public int read(byte[] r, int offset, int len){
			int bytesLeft = data.Length - readPos;
			if(bytesLeft < len) len = bytesLeft;
			Array.Copy(data, readPos, r, offset, len);
			readPos += len;
			return len;
		}
		
		public int readInt() {
			byte[] raw = new byte[4];
			read(raw, 0, raw.Length);
			return System.BitConverter.ToInt32(new byte[] { raw[3], raw[2], raw[1], raw[0] }, 0);
		}
		
		 public String readString(bool fullChar) {
			int c = readInt();
			//MyLog.Default.WriteLineAndConsole("writeString():len=" + c);
			char[] a = new char[c];
			char chr;
			for (int i = 0; i < c; i++) {
				chr = (char)0;
				if (fullChar) chr |= (char)((read()) << 8);
				chr |= (char)read();
				a[i] = chr;
			}
			//MyLog.Default.WriteLineAndConsole("writeString():out=" + new String(a));
			return new String(a);
		}
	}
}
