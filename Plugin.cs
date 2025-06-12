using BepInEx;
using LitJson;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Valve.Newtonsoft.Json;
using Valve.Newtonsoft.Json.Linq;
using static BoingKit.BoingWork;

namespace SelfTracker
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        static string GameStart;
        static string JoinRoom;
        static string LeaveRoom;
        static string WebhookURL;
        void Awake()
        {
            if (!Directory.Exists("SelfTracker"))
            {
                Directory.CreateDirectory("SelfTracker");
                Dictionary<string, string> Files = new Dictionary<string, string>
                {
                    { "GameStart.json", "{ \"content\": \"Game started at {0}\" }" },
                    { "JoinRoom.json", "{ \"content\": \"Connected to room {1} at {0} / Gamemode {4}, Players {2}/10, Region {3}\" }" },
                    { "LeaveRoom.json", "{ \"content\": \"Left room {1} at {0}\" }" },
                    { "Configuration.json", "{ \"WebhookURL\": \"https://discord.com/api/webhooks/...\"}" },
                    { "README.txt", "How to Use SelfTracker\n\n1. Input webhook URL into \"Configuration.Json\"\n2. Edit other json files to customize messages\n3. Start game\n\nFeel free to delete this file when finished" }
                };

                foreach (KeyValuePair<string, string> FileData in Files)
                    File.WriteAllText($"SelfTracker/{FileData.Key}", FileData.Value);

                Destroy(this); // Can't do much without no webhook /shrug
            } else
            {
                GameStart = File.ReadAllText("SelfTracker/GameStart.json");
                JoinRoom = File.ReadAllText("SelfTracker/JoinRoom.json");
                LeaveRoom = File.ReadAllText("SelfTracker/LeaveRoom.json");

                Dictionary<string, string> Data = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("SelfTracker/Configuration.json"));
                WebhookURL = Data["WebhookURL"];

                _ = SendWebhookAsync(string.Format(EscapeBracesExceptPlaceholders(GameStart), ToJsonString(ISO8601())));
                GorillaTagger.OnPlayerSpawned(OnPlayerSpawned);
            }
        }

        static bool inRoomStatus;
        static string lastRoom;
        static void OnJoinRoom()
        {
            if (inRoomStatus)
                return;

            inRoomStatus = true;
            lastRoom = PhotonNetwork.CurrentRoom.Name;

            _ = SendWebhookAsync(string.Format(EscapeBracesExceptPlaceholders(JoinRoom), ToJsonString(ISO8601()), ToJsonString(PhotonNetwork.CurrentRoom.Name), ToJsonString(PhotonNetwork.PlayerList.Length), ToJsonString(CleanString(PhotonNetwork.CloudRegion, 3)), ToJsonString(PhotonNetwork.CurrentRoom.CustomProperties["gameMode"])));
        }

        static void OnLeaveRoom()
        {
            if (!inRoomStatus)
                return;

            inRoomStatus = false;
            _ = SendWebhookAsync(string.Format(EscapeBracesExceptPlaceholders(LeaveRoom), ToJsonString(ISO8601()), ToJsonString(lastRoom)));
        }

        static void OnPlayerSpawned()
        {
            NetworkSystem.Instance.OnJoinedRoomEvent += OnJoinRoom;
            NetworkSystem.Instance.OnReturnedToSinglePlayer += OnLeaveRoom;
        }

        static string ISO8601()
        {
            DateTime utcNow = DateTime.UtcNow;
            return utcNow.ToString("o");
        }

        static string ToJsonString(object input)
        {
            Debug.Log(input.ToString());
            string jsonEscaped = JsonConvert.ToString(input?.ToString() ?? string.Empty);
            return jsonEscaped.Substring(1, jsonEscaped.Length - 2);
        }

        static string CleanString(string input, int maxLength = 12)
        {
            input = new string(Array.FindAll<char>(input.ToCharArray(), (char c) => Utils.IsASCIILetterOrDigit(c)));

            if (input.Length > maxLength)
                input = input.Substring(0, maxLength - 1);

            input = input.ToUpper();
            return input;
        }

        static string EscapeBracesExceptPlaceholders(string input)
        {
            input = Regex.Replace(input, @"\{(\d+)\}", "###PLACEHOLDER_$1###");
            input = input.Replace("{", "{{").Replace("}", "}}");
            input = Regex.Replace(input, @"###PLACEHOLDER_(\d+)###", "{$1}");

            return input;
        }

        static async Task SendWebhookAsync(string jsonData)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(WebhookURL, content);

                    if (!response.IsSuccessStatusCode)
                        Debug.LogError($"Webhook failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception while sending webhook: {ex}");
            }
        }
    }
}
