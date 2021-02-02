using ConVar;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("한글변환", "RedMat/UNKNOWN#2214", "2.5.1", ResourceId = 0)]
    [Description("영문타자 한글 변환")]
    class ConvertKor : CovalencePlugin
    {
        //업데이트 정보 보기
        //링크: https://github.com/krwolf76/ConvertKor

        private Dictionary<ulong, bool> convertKorUserSet;

        private Configuration _config;

        ForbiddenWordData forbiddenWordData;

        // 파라미터 1개 명령어 대문자로 설정
        Dictionary<String, int> cmdLists = new Dictionary<String, int>();

        private void Loaded()
        {

            convertKorUserSet = new Dictionary<ulong, bool>();

            // 파라미터 한개 명령어
            cmdLists.Add("C"    , 1);   // 클랜
            cmdLists.Add("A"    , 1);   // 동맹
            cmdLists.Add("R"    , 1);   // 귓말(답변)
            cmdLists.Add("PM"   , 2);   // 귓말


            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {
                String userId = basePlayer.UserIDString;
                if (!User.ContainsKey(userId))
                {
                    PlayerDatas data = new PlayerDatas();

                    data.WarnCount = 0;
                    data.Cooldown = 0;
                    data.KorMode = "EN";
                    data.UI = "활성화";
                    User.Add(userId, data);
                    Interface.Oxide.DataFileSystem.WriteObject("ConvertKor_PlayerData", User);
                }
            }

            User = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<String, PlayerDatas>>("ConvertKor_PlayerData");
            forbiddenWordData = Interface.Oxide.DataFileSystem.ReadObject<ForbiddenWordData>("ConvertKor_ForbiddenWordData");
        }

        void Unload()
        {
            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {

            }
            Interface.Oxide.DataFileSystem.WriteObject("ConvertKor_PlayerData", User);
        }

        void OnPlayerConnected(BasePlayer basePlayer)
        {
            if (basePlayer.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(2, () => OnPlayerConnected(basePlayer));

                if (_config.GUIToggle == false)
                {
                    basePlayer.IPlayer.Reply($"{string.Format(Lang("Prefix", null))} <color=red>서버에서 UI 를 비활성화 시켰습니다.</color>\n활성화: <color=#00ffff>/ui</color>");
                    CuiHelper.DestroyUi(basePlayer, "ChatUI");
                }
            }
            
            String userId = basePlayer.UserIDString;
            if (!User.ContainsKey(userId))
            {
                PlayerDatas data = new PlayerDatas();

                data.WarnCount = 0;
                data.Cooldown = 0;
                data.KorMode = "EN";
                data.UI = "활성화";
                User.Add(userId, data);
                DataSave();
            }
        }

        [HookMethod("OnBetterChat")]
        private object OnBetterChat(Dictionary<string, object> data)
        {
            String inMessage = (String)data["Message"];
            String convertMessage = "";

            IPlayer player = (IPlayer)data["Player"];
            
            BasePlayer basePlayer = BasePlayer.FindByID(ulong.Parse(player.Id));

            convertMessage = "KR".Equals(InfoUtils.GetUserMode(basePlayer, User[basePlayer.UserIDString])) ? StringUtils.getConvertKor(inMessage) : inMessage;

            
            LogChatFile("ConvertKor_Chat", convertMessage, player.Id, player.Name);
            
            Chat.ChatChannel chatChannel = (Chat.ChatChannel)data["ChatChannel"];

            if(chatChannel == 0)
            {
                LogChatFile("ConvertKor_Chat", convertMessage, player.Id, player.Name);
            }
            else
            {
                TeamLogChatFile("ConvertKor_Team", convertMessage, player.Id, player.Name);
            }
            data["Message"] = this.getChangeForbiddenWord(convertMessage, basePlayer, chatChannel);
            
            return data;
        }

        #region Hook
        [HookMethod("OnPlayerChat")]
        object OnPlayerChat(ConsoleSystem.Arg arg, Chat.ChatChannel chatchannel)
        {
            BasePlayer inputChatBasePlayer = arg.Connection.player as BasePlayer;
            String playerName = arg.Connection.username;
            String message = arg.GetString(0);
            String convertMessage = "KR".Equals(InfoUtils.GetUserMode(inputChatBasePlayer, User[inputChatBasePlayer.UserIDString])) ? StringUtils.getConvertKor(message) : message;

            // 콘솔로그
            Puts(playerName + ": " + convertMessage);

            // 욕설 변환
            convertMessage = this.getChangeForbiddenWord(convertMessage, inputChatBasePlayer, chatchannel);

            if (!isUsePlugin("BetterChat"))
            {
                if (chatchannel == Chat.ChatChannel.Team)
                {
                    List<Connection> sendUserList = new List<Connection>();
                    RelationshipManager.PlayerTeam team = inputChatBasePlayer.Team;
                    if (null == team || team.members.Count < 1) return true;
                    foreach (ulong teamUserId in team.members)
                    {
                        Connection inUser = BasePlayer.FindByID(teamUserId).Connection;
                        if(null != inUser) sendUserList.Add(inUser);
                    }
                    // 메시지 전송
                    if (sendUserList.Count > 0) ConsoleNetwork.SendClientCommand(sendUserList, "chat.add2", new object[] { chatchannel, inputChatBasePlayer.UserIDString, convertMessage, "[TEAM] " + inputChatBasePlayer.displayName, "#" + InfoUtils.GetUserNameColor() });
                }
                else
                {
                    List<Connection> sendUserList = new List<Connection>();
                    foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                    {
                        sendUserList.Add(basePlayer.Connection);
                    }
                    // 메시지 전송
                    if (sendUserList.Count > 0) ConsoleNetwork.SendClientCommand(sendUserList, "chat.add2", new object[] { chatchannel, inputChatBasePlayer.UserIDString, convertMessage, inputChatBasePlayer.displayName, "#" + InfoUtils.GetUserNameColor() });
                }

                return false;
            }

            return null;
        }

        private void sendChatMessage(Chat.ChatChannel chatchannel, List<BasePlayer> basePlayers, String message, BasePlayer inputUser)
        {
            LogChatFile("", message, inputUser.UserIDString, inputUser.displayName);

            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {
                basePlayer.SendConsoleCommand("chat.add2", new object[] {
                            chatchannel,
                            inputUser.UserIDString,
                            message,
                            inputUser.displayName,
                            "#" + InfoUtils.GetUserNameColor(),
                            1f
                        });
                /*
                basePlayer.SendConsoleCommand("chat.add", new object[] {
                            chatchannel,
                            inputUser,
                            message,
                            inputUser.displayName,
                            "#" + InfoUtils.GetUserNameColor()
                            //"<color=#" + InfoUtils.GetUserNameColor() + ">" + playerName + ": </color>" +
                            //"<color=#" + InfoUtils.GetUserMessageColor() + ">" + convertMessage + "</color>"
                        });
                        */
            }
        }

        private object OnPlayerCommand(BasePlayer basePlayer, string command, string[] args)
        {
            String message = "";
            String convertMessage = "";
            String upperCommand = command.ToUpper();

            if (cmdLists.ContainsKey(upperCommand))
            {
                // 메시지 조합
                for(int i = cmdLists[upperCommand] - 1; i < args.Length; i++)
                {
                    message += " " + args[i];
                }

                // 옵션
                string option = "";
                for (int i = 0; i < cmdLists[upperCommand] - 1; i++)
                {
                    option += " " + args[i];
                }

                convertMessage = StringUtils.getConvertKor(message);

                if (isConvertKor(basePlayer) && !isContainHangul(message))
                {
                    basePlayer.Command("chat.say", "/" + command + option + convertMessage);

                    return false;
                }
            }

            return null;
        }

            #endregion

        #region Command
        [Command("h")]
        private void chatCommandH(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = BasePlayerUtils.GetBasePlayer(ulong.Parse(player.Id));
            String userId = basePlayer.UserIDString;

            if (!convertKorUserSet.ContainsKey(basePlayer.userID)) convertKorUserSet.Add(basePlayer.userID, true);

            if (!User.ContainsKey(userId))
            {
                PlayerDatas data = new PlayerDatas();

                data.WarnCount = 0;
                data.Cooldown = 0;
                data.KorMode = "EN";
                data.UI = "활성화";
                User.Add(userId, data);
                Interface.Oxide.DataFileSystem.WriteObject("ConvertKor_PlayerData", User);
            }
            else
            {
                if("KR".Equals(User[userId].KorMode.ToString()))
                {
                    User[userId].KorMode = "EN";
                    CheckUI(basePlayer);
                    convertKorUserSet[basePlayer.userID] = false;
                    player.Reply($"{string.Format(Lang("Prefix", null))} <color=#00ffff>영어</color> 로 변경되었습니다.");
                }
                else
                {
                    User[userId].KorMode = "KR";
                    convertKorUserSet[basePlayer.userID] = true;
                    CheckUI(basePlayer);
                    player.Reply($"{string.Format(Lang("Prefix", null))} <color=#00ffff>한글</color> 로 변경되었습니다.");
                }
            }

            DataSave();
        }

        [Command("chatui")]
        private void ch1atCommandH(IPlayer player, string command, string[] args)
        {
            BasePlayer basePlayer = BasePlayerUtils.GetBasePlayer(ulong.Parse(player.Id));


            if("활성화".Equals(User[basePlayer.UserIDString].UI.ToString()))
            {
                CheckUI(basePlayer);
                player.Reply($"{string.Format(Lang("Prefix", null))} <color=#00ffff>UI 가 활성화 되었습니다.</color>");
                User[basePlayer.UserIDString].UI = "비활성화";
            }
            else
            {
                player.Reply($"{string.Format(Lang("Prefix", null))} <color=red>UI 가 비활성화 되었습니다.</color>");
                CuiHelper.DestroyUi(basePlayer, "ChatUI");
                User[basePlayer.UserIDString].UI = "활성화";
            }
            DataSave();
        }
        #endregion

        #region GUI
        void CheckUI(BasePlayer player)
        {
            #region AC 확인
            if (User[player.UserIDString].KorMode == "KR")
            {
                CuiHelper.DestroyUi(player, "ChatUI");
                if(_config.GUIToggle == true && User[player.UserIDString].UI == "활성화")
                {
                    GUI(player, string.Format(Lang("Korean", null)));
                }
                
            }
            if (User[player.UserIDString].KorMode == "EN")
            {
                CuiHelper.DestroyUi(player, "ChatUI");
                if (_config.GUIToggle == true && User[player.UserIDString].UI == "활성화")
                    GUI(player, string.Format(Lang("English", null)));
            }
            #endregion
        }
        void GUI(BasePlayer player, string msg = null)
        {
            var elements = new CuiElementContainer();

            var Notice = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = "0.29 0.49 0.69 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.001041667 0.125",
                    AnchorMax = "0.01145833 0.1666667"
                },
                CursorEnabled = false
            }, "Overlay", "ChatUI");
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = msg,
                    FontSize = 15,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, Notice);
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Data
        Dictionary<String, PlayerDatas> User = new Dictionary<String, PlayerDatas>();
        class PlayerDatas
        {
            public int WarnCount { get; set; }
            public int Cooldown { get; set; }
            public string KorMode { get; set; }

            public string UI { get; set; }

        }
        #endregion
        #region Classs
        public bool isContainHangul(string s)
        {
            char[] charArr = s.ToCharArray();
            foreach (char c in charArr)
            {
                if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherLetter) return true;
            }
            return false;
        }

        private Boolean isConvertKor(BasePlayer basePlayer)
        {
            if (!convertKorUserSet.ContainsKey(basePlayer.userID)) convertKorUserSet.Add(basePlayer.userID, true);

            return convertKorUserSet[basePlayer.userID];
        }

        private Boolean isUsePlugin(String pluginName)
        {
            foreach (Plugin plugin in plugins.GetAll())
            {
                if (pluginName == plugin.Name) return true;
            }

            return false;
        }

        private void LogChatFile(String fileName, String text, String playerId, String playerName)
        {
            LogToFile(fileName, $"[{DateTime.Now.ToString("HH:mm:ss")}][{playerId}]{playerName}: {text}", this);
        }

        private void TeamLogChatFile(String fileName, String text, String playerId, String playerName)
        {
            LogToFile(fileName, $"[{DateTime.Now.ToString("HH:mm:ss")}][Team][{playerId}]{playerName}: {text}", this);
        }

        private void DataSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ConvertKor_PlayerData", User);
        }

        class BasePlayerUtils
        {
            public static BasePlayer GetBasePlayer(ulong userID)
            {
                foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                {
                    if (basePlayer.userID == userID) return basePlayer;
                }

                return null;
            }

            public static BasePlayer GetBasePlayer(String userName)
            {
                foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                {
                    if (basePlayer.IPlayer.Name == userName) return basePlayer;
                }

                return null;
            }
        }

        class InfoUtils
        {
            public static String GetUserMode(BasePlayer basePlayer, PlayerDatas storedData)
            {
                String mode = "";
                string playerInfo = storedData.KorMode;
                if (null == playerInfo)
                {
                    mode = "KR";
                }
                else
                {
                    if (null == playerInfo)
                    {
                        mode = "KR";
                    }
                    else
                    {
                        mode = playerInfo.ToString();
                    }

                }

                return mode;
            }

            public static String GetUserNameColor()
            {
                return "ffaa55";
            }

            public static String GetUserMessageColor()
            {
                return "FFFFFF";
            }
        }

        private System.Object GetConvertKor(System.Object text)
        {
            if (null == text) return text;
            return StringUtils.getConvertKor(text.ToString());
        }

        private String getChangeForbiddenWord(String text, BasePlayer inputChatBasePlayer, Chat.ChatChannel chatChannel)
        {
            bool forbiddenWordAt = false;
            // 특문 제거
            String delCharMessage = Regex.Replace(text, @"[^a-zA-Z0-9가-힣]", "", RegexOptions.Singleline);

            String[] diffTexts = forbiddenWordData.word;

            foreach (String diffText in diffTexts)
            {
                if (delCharMessage.Contains(diffText))
                {
                    char[] diffTextArray = diffText.ToArray<char>();
                    char[] messageCharArray = text.ToArray<char>();

                    for (int i = 0; i < messageCharArray.Length; i++)
                    {
                        String cutText = Regex.Replace(text.Substring(i, text.Length - i), @"[^a-zA-Z0-9가-힣]", "", RegexOptions.Singleline);

                        if (cutText.Length < diffText.Length) break;

                        if (cutText.Substring(0, diffText.Length).Equals(diffText) && messageCharArray[i].Equals(diffTextArray[0]))
                        {
                            String endAt = "N";
                            int nextIndex = 0;

                            while ("N".Equals(endAt))
                            {
                                if (messageCharArray[i].Equals(diffTextArray[nextIndex]))
                                {
                                    forbiddenWordAt = true;
                                    messageCharArray[i] = char.Parse(_config.chatChangeChar);
                                    nextIndex++;
                                }

                                if (nextIndex == diffText.Length) endAt = "Y";
                                i++;
                            }
                        }
                    }
                    if (forbiddenWordAt && null != inputChatBasePlayer && inputChatBasePlayer.Connection.authLevel != 2)
                    {
                        if (Chat.ChatChannel.Global == chatChannel)
                        {
                            string userid = inputChatBasePlayer.UserIDString;

                            if (_config.WarnAutoShell == 1)
                            {
                                if (_config.AutoShell == 0)
                                {

                                }
                                else if (_config.AutoShell == 1)
                                {
                                   
                                    if(User[userid].WarnCount == _config.WarnCount)
                                    {
                                        server.Command($"bcm.mute {inputChatBasePlayer.userID} \"{string.Format(Lang("MuteReason", null, inputChatBasePlayer.displayName, _config.AutoShellTime))}\" {_config.AutoShellTime}");
                                        User[userid].WarnCount = 0;
                                    }
                                    User[userid].WarnCount += 1;
                                    DataSave();

                                }
                                else if (_config.AutoShell == 2)
                                {
                                    if (User[userid].WarnCount == _config.WarnCount)
                                    {
                                        server.Command($"kick {inputChatBasePlayer.userID} \"{string.Format(Lang("KickReason", null, inputChatBasePlayer.displayName))}\"", inputChatBasePlayer.displayName);
                                        User[userid].WarnCount = 0;
                                    }

                                    User[userid].WarnCount += 1;
                                    DataSave();
                                }
                                else if (_config.AutoShell == 3)
                                {
                                    if (User[userid].WarnCount == _config.WarnCount)
                                    {
                                        server.Command($"banid {inputChatBasePlayer.userID} {inputChatBasePlayer.displayName} \"{string.Format(Lang("BanReason", null, inputChatBasePlayer.displayName, _config.TimeBanSetting))}\" {_config.TimeBanSetting}");
                                        User[userid].WarnCount = 0;
                                    }
                                    User[userid].WarnCount += 1;
                                    DataSave();
                                }
                                else if (_config.AutoShell == 4)
                                {
                                    if (User[userid].WarnCount == _config.WarnCount)
                                    {
                                        server.Command($"banid {inputChatBasePlayer.userID} {inputChatBasePlayer.displayName} \"{string.Format(Lang("PBanReason", null, inputChatBasePlayer.displayName))}\"");
                                        User[userid].WarnCount = 0;
                                    }
                                        User[userid].WarnCount += 1;
                                    DataSave();
                                }
                            }
                            else if (_config.WarnAutoShell == 0)
                            {
                                if (_config.AutoShell == 0)
                                {

                                }
                                else if (_config.AutoShell == 1)
                                {
                                    server.Command($"bcm.mute {inputChatBasePlayer.userID} \"{string.Format(Lang("MuteReason", null, inputChatBasePlayer.displayName, _config.AutoShellTime))}\" {_config.AutoShellTime}");
                                }
                                else if (_config.AutoShell == 2)
                                {
                                    server.Command($"kick {inputChatBasePlayer.userID} \"{string.Format(Lang("KickReason", null, inputChatBasePlayer.displayName))}\"", inputChatBasePlayer.displayName);
                                }
                                else if (_config.AutoShell == 3)
                                {
                                    server.Command($"banid {inputChatBasePlayer.userID} {inputChatBasePlayer.displayName} \"{string.Format(Lang("BanReason", null, inputChatBasePlayer.displayName, _config.TimeBanSetting))}\" {_config.TimeBanSetting}");
                                }
                                else if (_config.AutoShell == 4)
                                {
                                    server.Command($"banid {inputChatBasePlayer.userID} {inputChatBasePlayer.displayName} \"{string.Format(Lang("PBanReason", null, inputChatBasePlayer.displayName))}\"");
                                }
                            }
                            

                        }
                    }

                    text = new string(messageCharArray);

                    text = forbiddenWordAt ? "" + text : text;
                }
            }
            if(_config.WordChangeToggle == true)
            {
                // 문자열 변환 
                foreach (String key in _config.WordChange.Keys)
                {
                    if (text.IndexOf(key) > -1)
                    {
                        //Puts("변환전 = " + text);
                        text = text.Replace(key, _config.WordChange[key]);
                        //Puts("변환후 = " + text);
                    }
                }
            }

            return text;
        }

        class StringUtils
        {
            public static Boolean isNotNull(String objectValue)
            {
                if (String.IsNullOrEmpty(objectValue)) return false;
                if (String.IsNullOrWhiteSpace(objectValue)) return false;
                return true;
            }

            public static String getConvertKor(String engText)
            {
                var res = "";
                if (engText.Length == 0)
                    return res;

                Int32 nCho = -1, nJung = -1, nJong = -1;        // 초성, 중성, 종성

                int intNotChange = 0;

                for (int i = 0; i < engText.Length; i++)
                {
                    string ch = engText[i].ToString();
                    int p = KorInfo.ENGLISH_KEY_LIST.IndexOf(ch);

                    if (engText.Length > 1 && (i + 2) <= engText.Length)
                    {
                        if ("##".Equals(engText.Substring(i, 2)))
                        {
                            intNotChange = intNotChange + 1;
                            i += 1;
                            continue;
                        }
                    }

                    if (intNotChange % 2 != 0)
                    {
                        res += engText.Substring(i, 1);
                        continue;
                    }

                    // 한글 표현 안되는 대문자 소문자로 변경
                    if (KorInfo.ENGLISH_DOWN_KEY_LIST.IndexOf(ch) >= 0)
                    {
                        p = KorInfo.ENGLISH_KEY_LIST.IndexOf(ch.ToLower());
                    }


                    int intMod = intNotChange % 2;

                    if (p == -1)
                    {               // 영자판이 아님
                                    // 남아있는 한글이 있으면 처리
                        if (nCho != -1)
                        {
                            if (nJung != -1)                // 초성+중성+(종성)
                                res += GetConvertString(nCho, nJung, nJong);
                            else                            // 초성만
                                res += KorInfo.KOREAN_FIRST_LIST[nCho];
                        }
                        else
                        {
                            if (nJung != -1)                // 중성만
                                res += KorInfo.KOREAN_MIDDLE_LIST[nJung];
                            else if (nJong != -1)           // 복자음
                                res += KorInfo.KOREAN_LAST_LIST[nJong];
                        }
                        nCho = -1;
                        nJung = -1;
                        nJong = -1;
                        res += ch;
                    }
                    else if (p < 19)
                    {           // 자음
                        if (nJung != -1)
                        {
                            if (nCho == -1)
                            {                   // 중성만 입력됨, 초성으로
                                res += KorInfo.KOREAN_MIDDLE_LIST[nJung];
                                nJung = -1;
                                nCho = KorInfo.KOREAN_FIRST_LIST.IndexOf(KorInfo.KOREAN_KEY_LIST[p]);
                            }
                            else
                            {                          // 종성이다
                                if (nJong == -1)
                                {               // 종성 입력 중
                                    nJong = KorInfo.KOREAN_LAST_LIST.IndexOf(KorInfo.KOREAN_KEY_LIST[p]);
                                    if (nJong == -1)
                                    {           // 종성이 아니라 초성이다
                                        res += GetConvertString(nCho, nJung, nJong);
                                        nCho = KorInfo.KOREAN_FIRST_LIST.IndexOf(KorInfo.KOREAN_KEY_LIST[p]);
                                        nJung = -1;
                                    }
                                }
                                else if (nJong == 0 && p == 9)
                                {           // ㄳ
                                    nJong = 2;
                                }
                                else if (nJong == 3 && p == 12)
                                {           // ㄵ
                                    nJong = 4;
                                }
                                else if (nJong == 3 && p == 18)
                                {           // ㄶ
                                    nJong = 5;
                                }
                                else if (nJong == 7 && p == 0)
                                {           // ㄺ
                                    nJong = 8;
                                }
                                else if (nJong == 7 && p == 6)
                                {           // ㄻ
                                    nJong = 9;
                                }
                                else if (nJong == 7 && p == 7)
                                {           // ㄼ
                                    nJong = 10;
                                }
                                else if (nJong == 7 && p == 9)
                                {           // ㄽ
                                    nJong = 11;
                                }
                                else if (nJong == 7 && p == 16)
                                {           // ㄾ
                                    nJong = 12;
                                }
                                else if (nJong == 7 && p == 17)
                                {           // ㄿ
                                    nJong = 13;
                                }
                                else if (nJong == 7 && p == 18)
                                {           // ㅀ
                                    nJong = 14;
                                }
                                else if (nJong == 16 && p == 9)
                                {           // ㅄ
                                    nJong = 17;
                                }
                                else
                                {                      // 종성 입력 끝, 초성으로
                                    res += GetConvertString(nCho, nJung, nJong);
                                    nCho = KorInfo.KOREAN_FIRST_LIST.IndexOf(KorInfo.KOREAN_KEY_LIST[p]);
                                    nJung = -1;
                                    nJong = -1;
                                }
                            }
                        }
                        else
                        {                              // 초성 또는 (단/복)자음이다
                            if (nCho == -1)
                            {                   // 초성 입력 시작
                                if (nJong != -1)
                                {               // 복자음 후 초성
                                    res += KorInfo.KOREAN_LAST_LIST[nJong];
                                    nJong = -1;
                                }
                                nCho = KorInfo.KOREAN_FIRST_LIST.IndexOf(KorInfo.KOREAN_KEY_LIST[p]);
                            }
                            else if (nCho == 0 && p == 9)
                            {           // ㄳ
                                nCho = -1;
                                nJong = 2;
                            }
                            else if (nCho == 2 && p == 12)
                            {           // ㄵ
                                nCho = -1;
                                nJong = 4;
                            }
                            else if (nCho == 2 && p == 18)
                            {           // ㄶ
                                nCho = -1;
                                nJong = 5;
                            }
                            else if (nCho == 5 && p == 0)
                            {           // ㄺ
                                nCho = -1;
                                nJong = 8;
                            }
                            else if (nCho == 5 && p == 6)
                            {           // ㄻ
                                nCho = -1;
                                nJong = 9;
                            }
                            else if (nCho == 5 && p == 7)
                            {           // ㄼ
                                nCho = -1;
                                nJong = 10;
                            }
                            else if (nCho == 5 && p == 9)
                            {           // ㄽ
                                nCho = -1;
                                nJong = 11;
                            }
                            else if (nCho == 5 && p == 16)
                            {           // ㄾ
                                nCho = -1;
                                nJong = 12;
                            }
                            else if (nCho == 5 && p == 17)
                            {           // ㄿ
                                nCho = -1;
                                nJong = 13;
                            }
                            else if (nCho == 5 && p == 18)
                            {           // ㅀ
                                nCho = -1;
                                nJong = 14;
                            }
                            else if (nCho == 7 && p == 9)
                            {           // ㅄ
                                nCho = -1;
                                nJong = 17;
                            }
                            else
                            {                          // 단자음을 연타
                                res += KorInfo.KOREAN_FIRST_LIST[nCho];
                                nCho = KorInfo.KOREAN_FIRST_LIST.IndexOf(KorInfo.KOREAN_KEY_LIST[p]);
                            }
                        }
                    }
                    else
                    {                                  // 모음
                        if (nJong != -1)
                        {                       // (앞글자 종성), 초성+중성
                                                // 복자음 다시 분해
                            Int32 newCho;           // (임시용) 초성
                            if (nJong == 2)
                            {                   // ㄱ, ㅅ
                                nJong = 0;
                                newCho = 9;
                            }
                            else if (nJong == 4)
                            {           // ㄴ, ㅈ
                                nJong = 3;
                                newCho = 12;
                            }
                            else if (nJong == 5)
                            {           // ㄴ, ㅎ
                                nJong = 3;
                                newCho = 18;
                            }
                            else if (nJong == 8)
                            {           // ㄹ, ㄱ
                                nJong = 7;
                                newCho = 0;
                            }
                            else if (nJong == 9)
                            {           // ㄹ, ㅁ
                                nJong = 7;
                                newCho = 6;
                            }
                            else if (nJong == 10)
                            {           // ㄹ, ㅂ
                                nJong = 7;
                                newCho = 7;
                            }
                            else if (nJong == 11)
                            {           // ㄹ, ㅅ
                                nJong = 7;
                                newCho = 9;
                            }
                            else if (nJong == 12)
                            {           // ㄹ, ㅌ
                                nJong = 7;
                                newCho = 16;
                            }
                            else if (nJong == 13)
                            {           // ㄹ, ㅍ
                                nJong = 7;
                                newCho = 17;
                            }
                            else if (nJong == 14)
                            {           // ㄹ, ㅎ
                                nJong = 7;
                                newCho = 18;
                            }
                            else if (nJong == 17)
                            {           // ㅂ, ㅅ
                                nJong = 16;
                                newCho = 9;
                            }
                            else
                            {                          // 복자음 아님
                                newCho = KorInfo.KOREAN_FIRST_LIST.IndexOf(KorInfo.KOREAN_LAST_LIST[nJong]);
                                nJong = -1;
                            }
                            if (nCho != -1)         // 앞글자가 초성+중성+(종성)
                                res += GetConvertString(nCho, nJung, nJong);
                            else                    // 복자음만 있음
                                res += KorInfo.KOREAN_LAST_LIST[nJong];

                            nCho = newCho;
                            nJung = -1;
                            nJong = -1;
                        }
                        if (nJung == -1)
                        {                       // 중성 입력 중
                            nJung = KorInfo.KOREAN_MIDDLE_LIST.IndexOf(KorInfo.KOREAN_KEY_LIST[p]);
                        }
                        else if (nJung == 8 && p == 19)
                        {            // ㅘ
                            nJung = 9;
                        }
                        else if (nJung == 8 && p == 20)
                        {            // ㅙ
                            nJung = 10;
                        }
                        else if (nJung == 8 && p == 32)
                        {            // ㅚ
                            nJung = 11;
                        }
                        else if (nJung == 13 && p == 23)
                        {           // ㅝ
                            nJung = 14;
                        }
                        else if (nJung == 13 && p == 24)
                        {           // ㅞ
                            nJung = 15;
                        }
                        else if (nJung == 13 && p == 32)
                        {           // ㅟ
                            nJung = 16;
                        }
                        else if (nJung == 18 && p == 32)
                        {           // ㅢ
                            nJung = 19;
                        }
                        else
                        {          // 조합 안되는 모음 입력
                            if (nCho != -1)
                            {           // 초성+중성 후 중성
                                res += GetConvertString(nCho, nJung, nJong);
                                nCho = -1;
                            }
                            else                        // 중성 후 중성
                                res += KorInfo.KOREAN_MIDDLE_LIST[nJung];
                            nJung = -1;
                            res += KorInfo.KOREAN_KEY_LIST[p];
                        }
                    }
                }

                // 마지막 한글이 있으면 처리
                if (nCho != -1)
                {
                    if (nJung != -1)            // 초성+중성+(종성)
                        res += GetConvertString(nCho, nJung, nJong);
                    else                        // 초성만
                        res += KorInfo.KOREAN_FIRST_LIST[nCho];
                }
                else
                {
                    if (nJung != -1)            // 중성만
                        res += KorInfo.KOREAN_MIDDLE_LIST[nJung];
                    else
                    {                      // 복자음
                        if (nJong != -1)
                            res += KorInfo.KOREAN_LAST_LIST[nJong];
                    }
                }

                return res;
            }

            private static String GetConvertString(Int32 koreanFirstIndex, Int32 koreanMiddleIndex, Int32 koreanLastIndex)
            {
                return new String(new char[] {
                    Convert.ToChar(0xac00   + koreanFirstIndex * 21 * 28
                                            + koreanMiddleIndex * 28
                                            + koreanLastIndex + 1)
                });
            }
        }

        class KorInfo
        {
            public static String ENGLISH_KEY_LIST = "rRseEfaqQtTdwWczxvgkoiOjpuPhynbml";
            public static String KOREAN_KEY_LIST = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅛㅜㅠㅡㅣ";
            public static String KOREAN_FIRST_LIST = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
            public static String KOREAN_MIDDLE_LIST = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
            public static String KOREAN_LAST_LIST = "ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";
            public static String ENGLISH_DOWN_KEY_LIST = "AZSXDCFVGBYHNUJMIKL";
        }
        #endregion


        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            //=> _config = new Configuration();
            _config = GetDefaultConfig();
        }
        protected override void SaveConfig() => Config.WriteObject(_config);

        private class Configuration
        {
            [JsonProperty("1. 자동 처리 (0 - 비활성화 / 1 - 채팅금지 / 2 - 강퇴 / 3 - 시간 밴 / 4 - 영구 밴)")]
            public int AutoShell { get; set; } = 0;

            [JsonProperty("1. 채팅 금지 자동 처리 시간 설정 (s - 초 / m - 분 / h - 시간 / d - 일 / w - 주")]
            public string AutoShellTime { get; set; } = "10m";

            [JsonProperty("1. 시간 밴 시간 설정 (시간고정됨)")]
            public int TimeBanSetting { get; set; } = 1;

            [JsonProperty("2. 경고 후 처리 (0 - 비활성화 / 1 - 활성화)")]
            public int WarnAutoShell { get; set; } = 0;

            [JsonProperty("2. 경고 횟수")]
            public int WarnCount { get; set; } = 5;

            [JsonProperty("3. 한/영 GUI 토글 (true - 활성화 / false - 비활성화)")]
            public bool GUIToggle { get; set; } = true;

            [JsonProperty("4. 비속어 필터링 설정(시발 -> **)")]
            public string chatChangeChar { get; set; } = "*";
            [JsonProperty("5. 단어집 토글 (true - 활성화 / false - 비활성화)")]
            public bool WordChangeToggle { get; set; } = true;

            [JsonProperty("5.1. 단어집 추가")]
            public Dictionary<string, string> WordChange;

        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                AutoShell = 0,
                WarnAutoShell = 0,
                AutoShellTime = "10m",
                TimeBanSetting = 1,
                GUIToggle = true,
                chatChangeChar = "*",
                WordChangeToggle = true,
                WordChange = new Dictionary<string, string>
                {
                    {"ㅋ", "킼"},
                    {"ㅎ", "핳"},
                    {"ㅇㅋ", "오키"},
                    {"ㅂㅇ", "바이"},
                    {"ㅎㅇ", "하이"},
                    {"ㄷ", "덜"},
                    {"ㄱ", "기억"},
                    {"ㅂㄷ", "부들"},
                    {"ㅜ", "T"},
                    {"ㅠ", "T"},
                    {"ㅈㅅ", "죄송"},
                    {"ㅆㄹ", "쏘리"},
                    {"ㄱㅊ", "괜츈"},
                    {"ㄴㄴ", "노노"},
                    {"ㅅㅇㄹ", "소오름"},
                    {"ㅇㅅㅇ", "응슷응"},
                    {"ㅇㅂㅇ", "응븝응"},
                    {"ㅇ_ㅇ", "응_응"}
                }
            };
        }

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MuteReason"] = "욕설감지 되었습니다. {0} {1}",
                ["KickReason"] = "{0} 님은 욕설 사용을 하여 강퇴 되었습니다.",
                ["BanReason"] = "{0} 님은 욕설 사용을 하여 {1} 시간 밴 되었습니다.",
                ["PBanReason"] = "{0} 님은 욕설을 사용하여 영구밴 되었습니다.",
                ["Korean"] = "<color=lime>한</color>",
                ["English"] = "<color=#00ffff>영</color>",
                ["Prefix"] = "[<b><color=#00ff00>한글</color></b>]",
                ["NoPermission"] = "<color=red>당신은 권한이 없습니다.</color>"
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }
        #endregion


        class ForbiddenWordData
        {
            public String[] word = new[] {
                "씨팔", "개새끼", "애미", "에미", "애비", "에비"
            };

            public ForbiddenWordData()
            {
            }
        }
    }
}
