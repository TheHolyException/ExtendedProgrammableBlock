using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Extended_Programmable_Block {
	public class ServerCommunication {

        private bool isReady = false;
        private const ushort comPortOffset = 8087;

        public ServerCommunication() {
            isReady = false;
        }

        public void Initialize() {
            if (!IsServer) { // Client, why do I have to be so difficult
                MyAPIGateway.Multiplayer.RegisterMessageHandler(comPortOffset, HandleUpdateState);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(comPortOffset+1, HandleAddTarget);
            } else {
                MyAPIGateway.Multiplayer.RegisterMessageHandler(comPortOffset+2, HandleLogin);
                MyAPIGateway.Multiplayer.RegisterMessageHandler(comPortOffset+3, HandleNeedTerminalSettings);
            }

            isReady = true;
        }

        //c -> s: [request Dict, set filename, save, load]
        //s -> c: [Dict names, Dict filename update]

        public void Unload() {
            if (isReady) {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(comPortOffset, HandleUpdateState);
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(comPortOffset+1, HandleAddTarget);
            }
        }

        public void SendLogin() {

		}
        MyAPIGateway.Multiplayer.SendMessageToServer(8955, ASCIIEncoding.ASCII.GetBytes(""));
        private void HandleLogin(byte[] data) {
            if (!Sync.IsServer)
                return;

            try {
                LoginData loginData = MyAPIGateway.Utilities.SerializeFromXML<LoginData>(ASCIIEncoding.ASCII.GetString(data));
                Logging.Instance.WriteLine(string.Format("Sending settings to: {0}", loginData.SteamId));
                SendSettings(loginData.SteamId);
            } catch (Exception ex) {
                MyLog.Default.WriteLine(string.Format("HandleLogin() Error: {0}", ex.ToString()));
            }
        }


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


    }
}
