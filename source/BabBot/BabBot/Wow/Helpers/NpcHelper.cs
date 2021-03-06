﻿using System;
using System.Collections.Generic;
using System.Text;
using BabBot.Common;
using BabBot.Manager;
using System.Threading;
using BabBot.States;
using BabBot.States.Common;
using Pather.Graph;
using System.Text.RegularExpressions;

namespace BabBot.Wow.Helpers
{
    #region Exceptions

    public class NpcInteractException : Exception
    {
        public NpcInteractException()
            : base("Unable open NPC dialog after " + 
                ProcessManager.AppConfig.MaxNpcInteractSec +
                " of waiting or it's service(s) unknown") { }

        public NpcInteractException(string err)
            : base(err) { }

    }

    public class GameObjectNotFountException : Exception
    {
        public GameObjectNotFountException(string name)
            : base("No Game Object found for name : " + name) {}
    }

    public class GameObjectCoordNotFound : Exception
    {
        public GameObjectCoordNotFound(string name)
            : base("No Coordinates found for Game Object : " + name) {}
    }

    public class CantReachNpcException : Exception
    {
        public CantReachNpcException(string name, string reason)
            : base("Can't reach NPC '" + name + "'. " + reason) { }
    }

    public class NpcProcessingException : Exception
    {
        public NpcProcessingException(string msg)
            : base(msg) { }
    }

    public class MultiZoneNotSupportedException : Exception
    {
        public MultiZoneNotSupportedException()
            : base("Multi-zone traveling not supported (yet)") { }
    }

    public class UnknownServiceException : Exception
    {
        public UnknownServiceException(string srv)
            : base("Unknown service :" + srv) { }
    }

    public class ServiceNotFound : Exception
    {
        public ServiceNotFound(string srv)
            : base("Service " + srv + " not found at target NPC") { }
    }

    public class BinderServiceNotFound : ServiceNotFound
    {
        public BinderServiceNotFound()
            : base("binder") { }
    }

    #endregion

    #region Supported Classes

    public class NpcDest
    {
        public NPC Npc { get; private set; }
        public Vector3D Waypoint { get; private set; }

        public void Init(NPC npc, Vector3D vector)
        {
            Npc = npc;
            Waypoint = vector;
        }
    }

    /// <summary>
    /// Class for Global Test state to test different states
    /// </summary>
    class TestGlobalState : State<WowPlayer>
    {
        State<WowPlayer> _state;

        public TestGlobalState(State<WowPlayer> state)
        {
            _state = state;
            _state.Finishing += ProcessManager.StopStateMachine;
        }

        /// <summary>
        /// Switch to assigned state
        /// </summary>
        /// <param name="player"></param>
        protected override void DoEnter(WowPlayer player)
        {
            CallChangeStateEvent(player, _state, false, true);
        }

        protected override void DoExecute(WowPlayer player) { }
    }

    #endregion

    /// <summary>
    /// Helper Class for generic NPC operations
    /// </summary>
    public static class NpcHelper
    {
        private delegate bool CheckDelegate(object[] param_list, out string[] res);

        // For test use
        public static bool UseState = true;

        static List<GameObject> SaveList = new List<GameObject>();

        internal static Dictionary<string, string> 
            ServiceFrameTable = new Dictionary<string, string>();
        
        static NpcHelper()
        {
            ServiceFrameTable.Add("gossip", "Gossip");
            ServiceFrameTable.Add("class_trainer", "ClassTrainer");
            ServiceFrameTable.Add("vendor", "Merchant");
            ServiceFrameTable.Add("banker", "Bank");
            ServiceFrameTable.Add("wep_skill_trainer", "Skill");
            ServiceFrameTable.Add("trade_skill_trainer", "TradeSkill");
            ServiceFrameTable.Add("battlemaster", "Battlefield");
            ServiceFrameTable.Add("taxi", "Taxi");
        }

        /// <summary>
        /// Execute lua call and return parameters
        /// Raise exception if lua call return null
        /// </summary>
        /// <returns></returns>
        private static string[] DoGetNpcDialogInfo()
        {
            return DoGetNpcDialogInfo(true);
        }

        private static string[] DoGetNpcDialogInfo(bool auto_close)
        {
            return LuaHelper.Exec("GetNpcDialogInfo", auto_close);
        }


        /// <summary>
        /// Interact with NPC if no Gossip frame open.
        /// Return type of open frame.
        /// If NPC has 1 quest or 1 service (vendor for ex) it might go to that frame directly
        /// so it not always returning available gossip options
        /// </summary>
        /// <param name="npc_name"></param>
        /// <returns>See description for "GetNpcFrameInfo" lua call</returns>
        public static string[] GetTargetNpcDialogInfo(string npc_name, string lfs)
        {
            return GetTargetNpcDialogInfo(npc_name, true, lfs);
        }

        public static string[] GetTargetNpcDialogInfo(
                        string npc_name, bool auto_close, string lfs)
        {
            string[] fparams = DoGetNpcDialogInfo(auto_close);
            string cur_service = fparams[0];

            if (cur_service == null)
            {
                fparams = InteractNpc(npc_name, auto_close, lfs);
                cur_service = fparams[0];
            }

            if (fparams == null)
                throw new NpcProcessingException("Unable retrieve NPC gossip information. " +
                            "Does it communicate at all ?");

            return fparams;
        }

        private static bool CheckNpcDialog(object[] param_list, out string[] fparams)
        {
            bool auto_close = (bool)param_list[0];
            fparams = DoGetNpcDialogInfo(auto_close);
            return fparams[0] != null;
        }

        /// <summary>
        /// Interact with NPC, wait until NPC frame opens and return type of open frame.
        /// See description for GetTargetNpcFrameInfo for detail
        /// Max Waiting delay configured by max_npc_interact_time parameter from app_config element
        /// </summary>
        /// <param name="npc_name"></param>
        /// <returns></returns>
        public static string[] InteractNpc(string npc_name, 
                            bool auto_close, string lfs)
        {
            Output.Instance.Log(lfs, "Interacting with NPC '" + npc_name + "' ...");

            // TODO Interact with Game Object by name
            LuaHelper.Exec("InteractWithTarget");

            // ProcessManager.Player.ClickToMoveInteract(ProcessManager.Player.CurTargetGuid);

            // IF NPC doesn't have any quests and just on gossip option the WoW
            // does use this single option by default
            
            /*
            DateTime dt = DateTime.Now;
            Thread.Sleep(100);
            TimeSpan ts = DateTime.Now.Subtract(dt);

            
            string[] fparams = null;
            string cur_service = null;
            while ((cur_service == null) &&
              (DateTime.Now.Subtract(dt).TotalMilliseconds <= 
                        ProcessManager.AppConfig.MaxNpcInteractTime))
            {
                Thread.Sleep(500);

                fparams = DoGetNpcDialogInfo(auto_close);
                cur_service = fparams[0];
            }

            if (cur_service == null)
                throw new NpcInteractException();
            */

            string[] fparams;
            TimedTargetInteract(CheckNpcDialog, new object[] { auto_close }, out fparams);
            return fparams;
        }

        public static GameObject FindGameObjByName(string name)
        {
            GameObject obj = DataManager.CurWoWVersion.GameObjData[name];
            if (obj == null)
                throw new GameObjectNotFountException(name);

            return obj;

        }

        public static void MoveToGameObjByName(string name, string lfs)
        {
            MoveToGameObj(FindGameObjByName(name), lfs);
        }

        public static Vector3D GetGameObjCoord(GameObject obj, string lfs)
        {
            WowPlayer player = ProcessManager.Player;

            Output.Instance.Debug(lfs, "Checking coordinates for Game Object. " +
                obj.Name + " ...");

            // Check if NPC located in the same zone
            int cid = 0;
            string zone_text = obj.ZoneText;
            try
            {
                cid = DataManager.ZoneList[zone_text].ContinentId;
            }
            catch
            {
                new ZoneNotFoundException(zone_text);
            }

            // Check if NPC has coordinates in the same continent
            if (cid != player.ContinentID)
                throw new CantReachNpcException(obj.Name,
                        "Game Object located on different continent.");
            else if (!zone_text.Equals(player.ZoneText))
                new MultiZoneNotSupportedException();

            // Check if NPC has any waypoints assigned
            if (!obj.BasePosition.IsValid())
                throw new CantReachNpcException(obj.Name,
                    "Game Object doesn't have any waypoints assigned. " +
                    "Check NPCData.xml and try again.");

            // By default check the base position
            Output.Instance.Debug(lfs, "Using Game Object waypoint: " + obj.BasePosition);

            return obj.BasePosition;
        }

        public static void MoveToGameObj(GameObject obj, string lfs)
        {
            Vector3D dest = GetGameObjCoord(obj, lfs);

            if (!MoveToDest(dest, lfs, "Moving to " + obj.FullName))
                throw new CantReachNpcException(obj.Name, "Game Object still away after " +
                    (ProcessManager.AppConfig.MaxTargetGetRetries + 1) + " tries");
        }


        public static bool MoveToDest(Vector3D dest, string lfs)
        {
            return MoveToDest(dest, lfs, "Moving to waypoint: " + dest);
        }

        public static void StartNavState(NavigationState ns, WowPlayer player, string lfs)
        {
            player.StateMachine.InitState = new TestGlobalState(ns);

            Output.Instance.Debug(lfs, "State: " +
                ProcessManager.Player.StateMachine.CurrentState);

            Output.Instance.Log(lfs, "Game Object coordinates located." +
                " Moving to Game Object using Player State Machine ...");

            // Wait state machine switch to new state
            while (!player.StateMachine.IsRunning)
                Thread.Sleep(100);

            // Wait state machine complete Navigation State
            while (player.StateMachine.IsRunning)
                Thread.Sleep(1000);
        }

        /// <summary>
        /// Move character to destination
        /// </summary>
        /// <param name="dest"></param>
        public static bool MoveToDest(Vector3D dest, string lfs, string tooltip_text)
        {
            WowPlayer player = ProcessManager.Player;

            if (UseState)
            {
                // Use NavigationState
                NavigationState ns = new NavigationState(dest, lfs, tooltip_text);

                if (player.StateMachine.IsRunning)
                {
                    // Assume bot doesn't do anything and wait
                    // for manual state change
                    // Otherwise we can't call this method outside of bot thread
                    player.StateMachine.SafeStateChange(ns);
                }
                else
                {
                    StartNavState(ns, player, lfs);
                }
            }
            else
            {
                float distance = dest.GetDistanceTo(player.Location);

                // We have a 3 tries by default to reach target NPC
                int retry = 0;
                int max_retry = ProcessManager.AppConfig.MaxTargetGetRetries;

                // We might be already at the target
                while ((distance > 5F) && (retry <= max_retry))
                {
                    if (retry > 0)
                        Output.Instance.Log("Retrying " + retry + " of " + 
                                max_retry + " to reach the destination");
                    // Click to move on each waypoint
                    // dynamically calculate time between each click 
                    // based on distance to the next waypoint

                    // Lets calculate path from character to NPC and move
                    Path p = ProcessManager.Caronte.CalculatePath(
                        WaypointVector3DHelper.Vector3DToLocation(ProcessManager.Player.Location),
                        WaypointVector3DHelper.Vector3DToLocation(dest));

                    // Travel path
                    int max = p.Count;
                    Vector3D vprev = dest;
                    Vector3D vnext;
                    for (int i = 0; i < max; i++)
                    {
                        vnext = WaypointVector3DHelper.LocationToVector3D(p.Get(i));

                        // Calculate travel time
                        distance = vprev.GetDistanceTo(vnext);
                        int t = (int)((distance / 7F) * 1000);

                        ProcessManager.Player.ClickToMove(vnext);
                        Thread.Sleep(t);
                        vprev = vnext;
                    }
                }

                distance = dest.GetDistanceTo(player.Location);

                if (distance < 5F)
                    retry++;
            }

            return (dest.GetDistanceTo(player.Location) <= 5F);
        }

        public static void TargetGameObj(GameObject obj, string lfs)
        {
            

            // Only can target NPC
            if ((obj.ObjType == DataManager.GameObjectTypes.NPC) &&
                    !LuaHelper.TargetUnitByName(obj.Name))
                throw new QuestSkipException("Unable target NPC");
        }

        public static string[][] GetGossipList(string lfs)
        {
            // Get list of options
            string[][] res = null;
            Output.Instance.Debug(lfs, "Getting list of gossip options ...");

            string[] opts = ProcessManager.
                Injector.Lua_ExecByName("GetGossipOptions");
            if (opts == null || (opts.Length == 0))
                Output.Instance.Debug("No service detected.");
            else
            {
                int max = (int)(opts.Length / 2);
                Output.Instance.Debug(max + " gossip(s) detected.");
                res = new string[][] { new string[max], new string[max] };

                // Parse list of services
                for (int i = 0; i < max; i++)
                {
                    res[0][i] = opts[i * 2 + 1];
                    res[1][i] = opts[i * 2];
                }
            }
            return res;
        }

        private static void SelectNpcGossipOption(NPC npc, int idx, string lfs)
        {
            SelectNpcOption(npc, "SelectGossipOption", idx, lfs);
        }
    
        /// <summary>
        /// Check list of available NPC gossip options and click on each
        /// </summary>
        /// <param name="npc">NPC</param>
        /// <returns>true if NPC has any service</returns>
        private static bool CheckAvailGossips(NPC npc, string lfs)
        {
            // Get list of options
            string[][] opts = GetGossipList(lfs);

            if (opts == null || (opts[0].Length > 0))
            {
                Output.Instance.Log("Checking each available gossip option");
                // TODO We only need test standard service but not talking one, especially teleporting one

                for (int i = 0; i < opts[0].Length; i++)
                {
                    string cur_opts = opts[0][i];
                    string cur_descr = opts[1][i];

                    // Select only options that brings extra dialog
                    // like vendor
                    switch (cur_opts)
                    {
                        case "gossip":
                            // Check if it's binding options
                            npc.AddGossip(cur_descr, DataManager.CurWoWVersion.
                                        NpcConfig.PortOptionRx.IsMatch(cur_descr));
                            break;

                        case "vendor":
                        case "trainer":
                            Output.Instance.Debug("Selecting gossip option: " + i + "; " + cur_opts);
                            SelectNpcOption(npc, "SelectGossipOption", i + 1, lfs);
                            break;

                        case "binder":
                        case "taxi":
                        case "banker":
                        case "battlemaster":
                            AddNpcService(npc, cur_opts, null, lfs);
                            break;

                        default:
                            throw new UnknownServiceException(cur_opts);

                    }
                }
            }

            return true;
        }

        public static bool SelectNpcOption(NPC npc, string lua_fname, int idx, string lfs)
        {
            // Choose gossip option and check npc again
            Output.Instance.Debug("Execute: " + lua_fname + "; idx: " + idx);
            LuaHelper.Exec(lua_fname, idx);

            if (!AddTargetNpcInfo(npc, lfs))
                return false;

            // After gossip option processed interact with npc again
            // if (idx < max)
            // NpcHelper.InteractNpc(npc.Name);

            return true;
        }

        public static bool CheckDialogOpen(object[] param_list, out string[] res)
        {
            res = null;
            string fname = (string)param_list[0];
            return LuaHelper.Exec("IsNpcFrameOpen", fname)[0].Equals("1");
        }

        public static void WaitDialogOpen(string fname, string lfs)
        {
            Output.Instance.Debug(lfs, 
                "Waiting for the " + fname + "Frame opened");

            /*
            bool is_open = false;
            DateTime dt = DateTime.Now;

            do
            {
                Thread.Sleep(1000);
                is_open = LuaHelper.Exec("IsNpcFrameOpen", fname)[0].Equals("1");
            } while (!is_open &&
                ((DateTime.Now - dt).TotalMilliseconds <=
                    ProcessManager.AppConfig.MaxNpcInteractTime));

            if (!is_open)
                throw new NpcInteractException(
                    "Unable retrieve " + fname + "Frame after " +
                        ProcessManager.AppConfig.MaxNpcInteractSec + 
                        " of waiting");

             */
            TimedTargetInteract(CheckDialogOpen, new object[] { fname }, 
                        "Unable retrieve " + fname + "Frame after " +
                        ProcessManager.AppConfig.MaxNpcInteractSec +
                        " of waiting");
            Output.Instance.Debug(lfs,fname + "Frame ready");
        }

        #region Add NPC

        public static GameObject AddNpc(string lfs)
        {
            // Initialize parameters
            SaveList.Clear();

            string npc_name = ProcessManager.Player.CurTarget.Name;
            ProcessManager.Player.SetCurrentMapInfo();

            string[] npc_info = ProcessManager.Injector.
                Lua_ExecByName("GetUnitInfo", new object[] { "target" });

            Output.Instance.Log("Checking NPC Info ...");
            NPC npc = new NPC(ProcessManager.Player, npc_info[2]);

            if (!AddTargetNpcInfo(npc, lfs))
                return null;

            SaveList.Add(npc);

            if (DataManager.MergeGameObjData(SaveList, true, "npc"))
            {
                Output.Instance.Log(lfs, "NPC '" + npc.Name +
                    "' successfully added to " + DataManager.GameObjDataFileName);

                return DataManager.CurWoWVersion.GameObjData[npc.Name];
            }

            return null;
        }

        /// <summary>
        /// Check list of quest available on NPC gossip window and click on each
        /// to assign with NPC
        /// </summary>
        /// <param name="npc"></param>
        /// <returns></returns>
        private static bool AddQuests(NPC npc, string lfs, string type)
        {
            Output.Instance.Debug(lfs, "Checking " + type.ToLower() + " quests ...");

            // Get list of quests
            string[] quests = QuestHelper.GetGossipQuests(type);

            if (quests == null || (quests.Length == 0))
                Output.Instance.Debug(lfs, "No " + type.ToLower() + " quests detected.");
            else
            {
                int cnt = (int)(quests.Length / 3);
                Output.Instance.Debug(lfs, cnt + " quests(s) detected.");


                // Parse list of quests
                for (int i = 0; i < cnt; i++)
                {
                    string qtitle = quests[i * 3];

                    // for active quests lookup local log 
                    // and mark quest destination as current NPC
                    if (type.Equals("Active"))
                        AddNpcQuestEnd(npc.Name, qtitle, lfs);
                    else
                    {
                        // For available quests we need the whole story
                        try
                        {
                            // Last parameter we not interested in
                            Output.Instance.Debug(lfs, "Adding quest '" +
                                                            qtitle + "'");

                            SelectNpcOption(npc,"SelectGossip" + type + "Quest", i + 1, lfs);
                        }
                        catch (Exception e)
                        {
                            Output.Instance.LogError(lfs, "Error selecting quest. " + e.Message);
                            return false;
                        }
                    }

                }
            }

            return true;
        }

        // This method is recursive
        private static bool AddTargetNpcInfo(NPC npc, string lfs)
        {
            string[] fparams = GetTargetNpcDialogInfo(npc.Name, lfs);

            string cur_service = fparams[0];

            if (cur_service == null)
            {
                Output.Instance.Log("NPC is useless. No services or quests are detected");
                return true;
            }

            if (cur_service.Equals("gossip"))
            {
                Output.Instance.Debug(lfs, "GossipFrame opened.");

                Output.Instance.Debug(lfs, "Checking available options ...");
                // Get number of gossips and quests
                string[] opts = ProcessManager.
                    Injector.Lua_ExecByName("GetNpcGossipInfo");

                // Convert result to int format
                int[] opti = new int[opts.Length];
                for (int i = 0; i < opti.Length; i++)
                {
                    try
                    {
                        opti[i] = Convert.ToInt32(opts[i]);
                    }
                    catch (Exception e)
                    {
                        throw new NpcProcessingException("Unable convert " + i + " parameter '" +
                            opts[i] + "' from  'GetNpcGossipInfo' result to integer. " + e);
                    }
                }

                if ((opti[0] > 0) || (opti[1] > 0))
                { 
                    if (!AddQuests(npc, lfs, ((opti[0] > 0) ? "Available" : "Active")))
                        return false;
                }

                if (opti[2] > 0)
                    if (!CheckAvailGossips(npc, lfs))
                        return false;

            }
            else if (cur_service.Equals("quest_start"))
                AddNpcQuestStart(npc, fparams, lfs);
            else if ((cur_service.Equals("quest_progress")) ||
                     (cur_service.Equals("quest_end")))
                AddNpcQuestEnd(npc.Name, fparams[1], lfs);
            else
                AddNpcService(npc, cur_service, fparams, lfs);

            return true;
        }


        private static void AddNpcQuestStart(NPC npc, string[] opts, string lfs)
        {
            Quest q = null;

            // Check parsing result
            for (int i = 1; i < 4; i++)
            {
                string s = opts[i];

                if (string.IsNullOrEmpty(opts[i]))
                {
                    Output.Instance.LogError(lfs, i +
                        " parameter from quest info result is empty");
                    return;
                }

                // Check each value against pattern
                Regex rx = DataManager.CurWoWVersion.QuestConfig.Patterns[i - 1];
                if (!rx.IsMatch(s))
                {
                    Output.Instance.LogError(lfs, i +
                        " parameter '" + s + "' from quest scan" +
                        " doesn't match template '" + rx.ToString() + "'");
                    return;
                }
            }

            // Read header
            string[] headers = opts[1].Split(new string[] { "::" }, StringSplitOptions.None);
            string qtitle = headers[0];

            // At this point we staying in front of NPC and quest opened
            // Accepting quest and reading it info from toon's quest log
            try
            {
                QuestHelper.DoAction(QuestHelper.MakeAcceptQuestReq(), lfs);

                // Look on quest log for all details
                string[] ret = ProcessManager.Injector.
                    Lua_ExecByName("GetLogQuestInfo", new string[] { qtitle });

            
                // Parse result
                string[] info = opts[2].Split(',');
                string[] details = opts[3].Split(new string[] { "||" }, StringSplitOptions.None);

            
                Output.Instance.Log("Assign current NPC as start for quest '" + qtitle + "'");

                q = new Quest(Convert.ToInt32(ret[1]), qtitle,
                    headers[1], headers[2], Convert.ToInt32(ret[2]),
                    new int[] { Convert.ToInt32(info[3]), Convert.ToInt32(info[4]), 
                    Convert.ToInt32(info[5]) }, details, ret[4], info[0], ret[3]);
            }
            catch (Exception e)
            {
                Output.Instance.LogError(lfs, "Error creating quest with parameters " +
                    "Header: " + headers[1] + "; Text: " + headers[2] +
                    "; Level: " + headers[3], e);
                throw new QuestProcessingException("Failed add quest" + e.Message);
            }
            finally
            {
                // Whatever happened need abandon quest
                QuestHelper.AbandonQuest(qtitle, lfs);
            }

            if (q != null)
            {
                // Screen in npcdata how many quests with same title exists
                Quest mq = DataManager.CurWoWVersion.GameObjData.FindMaxQuestByTitle(q.Title);
                if (mq != null)
                {
                    q.QNum = mq.QNum + 1;
                    // Probably I need link to high lvl quest with same title
                    q.Relations.Add(q.Id);
                }
                npc.AddQuest(q);
            }
        }

        private static void AddNpcService(NPC npc,
            string cur_service, string[] opts, string lfs)
        {
            NPCService npc_service = null;

            switch (cur_service)
            {
                case "class_trainer":
                    npc_service = new ClassTrainingService(ProcessManager.Player.CharClass);
                    break;

                case "trade_skill_trainer":
                    npc_service = new TradeSkillTrainingService("");
                    break;

                case "vendor":
                    npc_service = new VendorService(
                            ((opts[1] != null) && opts[1].Equals("1")),
                            ((opts[2] != null) && opts[2].Equals("1")),
                            ((opts[3] != null) && opts[3].Equals("1")));
                    break;

                case "taxi":
                case "binder":
                    npc_service = new ZoneNpcService(cur_service, ProcessManager.Player.SubZoneText);
                    break;

                case "wep_skill_trainer":
                case "banker":
                case "battlemaster":
                    npc_service = new NPCService(cur_service);
                    break;

                default:
                    Output.Instance.Log(lfs, "Unknown npc service type '" +
                                cur_service + "'");
                    break;
            }

            if (npc_service != null)
                npc.AddService(npc_service);
        }

        private static bool AddNpcQuestEnd(string npc_name, string qtitle, string lfs)
        {
            bool res = false;

            // Lookup local log for quest details
            string[] ret = ProcessManager.Injector.
                Lua_ExecByName("FindLogQuest", new string[] {qtitle});

            int id = 0;
            int idx = 0;
            try
            {
                idx = Convert.ToInt32(ret[0]);
                id = Convert.ToInt32(ret[1]);
            }
            catch {}

            if (idx == 0)
                throw new QuestProcessingException("Unable retrieve information from " + 
                    "toon's log for quest '" + qtitle + "'");
            else if (id == 0)
                throw new QuestProcessingException("Unable retrieve quest id for " + 
                    " quest '" + qtitle + "'");

            // Now find NPC who has an original quest
            Quest q = DataManager.FindQuestById(id);

            if (q != null)
            {
                Output.Instance.Log("Assign current NPC as end for quest '" +
                                                                qtitle + "'");

                // After this quest should be marked as Changed and go on export
                q.DestName = npc_name;

                res = true;
            } else
                Output.Instance.Log("Unable locate quest giver for quest '" +
                                                                qtitle + "'");

            return res;
        }

        public static NPC MoveInteractService(string service, string lfs)
        {
            NpcDest dest = FindNearestService(service);
            if (dest == null)
                throw new ServiceNotFountException(service);

            // Move to NPC
            // If it failed it throw exception. I know :)
            MoveToDest(dest.Waypoint, lfs, "Moving to NPC: '" + dest.Npc.Name + "'");

            // Interact with NPC and select given service
            LuaHelper.TargetUnitByName(dest.Npc.Name);
            InteractNpc(dest.Npc.Name, false, lfs);

            return dest.Npc;
        }


        private static NpcDest FindNearestService(string service)
        {
            List<NPC> list = new List<NPC>();
            int cid = ProcessManager.Player.ContinentID;
            string player_zone = (string) ProcessManager.Player.ZoneText;
            Vector3D loc = (Vector3D) ProcessManager.Player.Location.Clone();

            // First check if service local
            try
            {
                ZoneServices local_srv = DataManager.ZoneList[player_zone];

                if (local_srv != null)
                {
                    // Now check if service local
                    list = local_srv.LocalServices[service];
                }
            } catch 
            {
                // Its not local service
            }

            
            
            if (list == null)
            {
                // Otherwise look the whole list
                foreach (GameObject obj in DataManager.CurWoWVersion.GameObjData.Values)
                {
                    if (obj.ObjType == DataManager.GameObjectTypes.ITEM)
                        continue;

                    NPC npc = (NPC)obj;

                    if (npc.Services.ContainsKey(service))
                        list.Add(npc);
                }
            }

            if (list.Count == 0)
                return null;

            // Find closest
            float dist = float.MaxValue;
            NpcDest res = null;

            foreach (NPC npc in list)
            {
                // Check if NPC on the same continent/zone
                if (!player_zone.Equals(npc.ZoneText))
                    throw new MultiZoneNotSupportedException();

                res = new NpcDest();

                float d1 = loc.GetDistanceTo(npc.BasePosition);

                if (d1 < dist)
                {
                    res.Init(npc, npc.BasePosition);
                    dist = d1;
                } 

                foreach (ZoneWp z in npc.Coordinates.Values)
                {
                    if (!z.Name.Equals(player_zone))
                        continue;

                    foreach (Vector3D v in z.List)
                    {
                        // Calculate straight distance to each NPC waypoints
                        float dist1 = loc.GetDistanceTo(v);
                        if (dist1 < dist)
                        {
                            dist = dist1;
                            res.Init(npc, v);
                        }
                    }
                }
            }

            return res;
        }

        #endregion

        #region Skills

        private static void SelectService(string npc_name, string service, string lfs)
        {
            string dialog = null;

            try
            {
                dialog = ServiceFrameTable[service];
            }
            catch
            {
                throw new UnknownServiceException(service);
            }


            // Get open Frame and make sure it's the correct one
            string[] dinfo = DoGetNpcDialogInfo(false);
            if (!dinfo[0].Equals(service))
            {
                // Get available services
                string[][] glist = GetGossipList(lfs);

                if ((glist == null) || (glist[0].Length == 0))
                    throw new ServiceNotFound(service);

                string[] srv = glist[0];

                bool f = false;
                for(int i = 0; i < srv.Length; i++)
                {
                    if (srv[i].Equals(service))
                    {
                        Output.Instance.Debug("Selecting service: " + service + " at index: " + i);
                        
                        LuaHelper.Exec("SelectGossipOption", i + 1);
                        Output.Instance.Debug("Waiting " + service + " dialog to open");

                        WaitDialogOpen(dialog, lfs);
                        f = true;
                    }
                }

                if (!f)
                    throw new ServiceNotFound(service);

            }

        }

        public static void LearnSkills(NPC npc, string skill, string lfs)
        {
            SelectService(npc.Name, skill, lfs);
            // Filter by available only
            LuaHelper.Exec("SetTrainerAvailableFilter");
            // Wait few sec to apply filter
            Thread.Sleep(2000);

            // Do until all we can afford is learned
            bool done;
            do
            {
                // Get # of avail services
                string[] dinfo = LuaHelper.Exec("GetServiceIdxList");
                string cur_set = dinfo[0];
                done = string.IsNullOrEmpty(cur_set);
                if (!done)
                {
                    string[] set1 = cur_set.Split(new string[] { "::" },
                                    StringSplitOptions.RemoveEmptyEntries);
                    string[] set2 = set1[0].Split(',');

                    int idx;
                    try
                    {
                        idx = Convert.ToInt32(set2[0]);
                    }
                    catch
                    {
                        throw new Exception("Unable recognize skill index: " + set2[0] + 
                            " from class training service list");
                    }


                    Output.Instance.Log(lfs, "Learning: " + 
                                        set2[1] + " at idx: " + idx);
                    LuaHelper.Exec("BuyTrainerService", idx);
                    // Wait few sec to apply
                    Thread.Sleep(2000);
                }
            } while (!done); 

            // todo close class frame
        }

        #endregion

        public static void BindToInn(NPC npc, string lfs)
        {
            // Check if NPC targeted
            WowUnit target = ProcessManager.Player.CurTarget;
            if (target == null || !target.Name.Equals(npc.Name))
                TargetGameObj(npc, lfs);

            // Check NPC has bind service
            if (!npc.HasInn)
                throw new BinderServiceNotFound();

            // Interact with NPC and choose binder service
            string[] fparams = NpcHelper.GetTargetNpcDialogInfo(npc.Name, lfs);

            string cur_service = fparams[0];

            if ((cur_service == null) || !cur_service.Equals("gossip"))
                throw new BinderServiceNotFound();

            // Get list of options
            string[][] opts = GetGossipList(lfs);
            if (opts == null)
                throw new BinderServiceNotFound();

            // Find binder
            int idx = 0;
            for (int i = 0; i < opts[0].Length; i++)
            {
                if (opts[0][i].Equals("binder"))
                {
                    idx = i + 1;
                    break;
                }
            }

            if (idx == 0)
                throw new BinderServiceNotFound();

            // Click make inn my home
            SelectNpcGossipOption(npc, idx, lfs); 

            // Wait for dialog popup
            string[] dname;
            TimedTargetInteract(CheckBinderConfirmation, out dname);

            if (string.IsNullOrEmpty(dname[0]))
                throw new BinderServiceNotFound();

            // Confirm bind
            LuaHelper.Exec("ConfirmBinder", dname[0]);
        }

        private static bool CheckBinderConfirmation(object[] param_list, out string[] res)
        {
            return CheckStaticDialog("CONFIRM_BINDER", out res);
        }

        private static bool CheckStaticDialog(string dname, out string[] res)
        {
            res = LuaHelper.Exec("FindVisibleStaticDialog", dname);
            return !string.IsNullOrEmpty(res[0]);
        }


        #region TimedTargetInteract

        private static void TimedTargetInteract(CheckDelegate check)
        {
            string[] res;
            TimedTargetInteract(check, null, out res, null);
        }

        private static void TimedTargetInteract(CheckDelegate check, out string[] res)
        {
            TimedTargetInteract(check, null, out res, null);
        }

        private static void TimedTargetInteract(CheckDelegate check,
                                    object[] param_list, string err)
        {
            string[] res;
            TimedTargetInteract(check, param_list, out res, err);
        }

        private static void TimedTargetInteract(CheckDelegate check, 
                                    object[] param_list, out string[] res)
        {
            TimedTargetInteract(check, param_list, out res, null);
        }

        private static void TimedTargetInteract(CheckDelegate check,
                                    object[] param_list, out string[] res, string err)
        {
            TimedTargetInteract(check, param_list, out res, 1000, err);
        }

        private static void TimedTargetInteract(CheckDelegate check, 
                                    object[] param_list, out string[] res, int wait, string err)
        {
            res = null;
            bool f = false;
            DateTime dt = DateTime.Now;
            while (!f && (DateTime.Now - dt).
                TotalMilliseconds <= ProcessManager.AppConfig.MaxNpcInteractTime)
            {
                Thread.Sleep(wait);
                f = check(param_list, out res);
            }

            if (!f)
            {
                if (err == null)
                    throw new NpcInteractException();
                else
                    throw new NpcInteractException(err);
            }
        }

        #endregion
    }
}
