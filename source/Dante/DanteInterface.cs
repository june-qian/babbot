﻿/*
    This file is part of BabBot.

    BabBot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    BabBot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with BabBot.  If not, see <http://www.gnu.org/licenses/>.
  
    Copyright 2009 BabBot Team -
*/

using System;
using System.Collections.Generic;

namespace Dante
{
    public class DanteInterface : MarshalByRefObject
    {
        public void DoString(string command)
        {
            try
            {
                LuaInterface.LoggingInterface.Log(string.Format(
                    "DoString() - State: {0}; Executing: {1} ...", LuaInterface.LuaState, command));
                LuaInterface.PendingDoString = command;

                LuaInterface.LoggingInterface.Log(string.Format("DoString() - Done"));

                // Always last
                LuaInterface.LuaState = 1;

            }
            catch(Exception e)
            {
                LuaInterface.LoggingInterface.Log("DoString() - Exception:" + e);
                LuaInterface.LuaState = 255;
            }
        }

        public void DoStringEx(string command)
        {
            lock (LuaInterface.dataLock)
            {
                try
                {
                    LuaInterface.LoggingInterface.Log(string.Format(
                                                          "DoStringEx() - State: {0}; Executing (\"{1}\") ...",
                                                          LuaInterface.LuaState, command));

                    LuaInterface.PendingDoString = command;
                    LuaInterface.LoggingInterface.Log(string.Format("DoStringEx() - Done"));

                    LuaInterface.Values.Clear();
                    LuaInterface.ValueReceived = false;

                    // Always last
                    LuaInterface.LuaState = 2;
                }
                catch (Exception e)
                {
                    LuaInterface.LoggingInterface.Log(
                        "DoStringInputHandler() - Exception: " + e.ToString());
                }
            }
        }

        public List<string> GetValues()
        {
            lock (LuaInterface.dataLock)
            {
                try
                {
                    bool val = LuaInterface.ValueReceived;
                    LuaInterface.LoggingInterface.Log(string.Format(
                                                          "GetValues() - ValueReceived: {0}; Calling ...", val));

                    if (val)
                    {
                        List<string> retValues = new List<string>();
                        foreach (string s in LuaInterface.Values)
                        {
                            LuaInterface.LoggingInterface.Log("GetValues() Value [" + s + "]");
                            retValues.Add(s);
                        }

                        LuaInterface.LoggingInterface.Log(string.Format(
                                                              "GetValues() - Done returned {0} parameters",
                                                              LuaInterface.Values.Count));
                        return retValues;
                    }

                    LuaInterface.LoggingInterface.Log("GetValues() - Done no value(s) received");

                }
                catch (Exception e)
                {
                    LuaInterface.LoggingInterface.Log("GetValues() exception: " + e.ToString());
                }
            }
            return new List<string>();
        }

        public bool IsLuaRequestCompleted()
        {
            lock (LuaInterface.dataLock)
            {
                try
                {
                    byte lstate = LuaInterface.LuaState;
                    LuaInterface.LoggingInterface.Log(string.Format(
                                                          "IsLuaRequestCompleted() - State: " + lstate));
                    return (lstate == 255);
                }
                catch (Exception e)
                {
                    LuaInterface.LoggingInterface.Log("IsLuaRequestCompleted() - Exception: " + e.ToString());
                    LuaInterface.LuaState = 255;
                    return true;
                }
            }
        }

        public bool IsDoStringHasValue()
        {
            lock (LuaInterface.dataLock)
            {
                byte lstate = LuaInterface.LuaState;
                bool vstate = LuaInterface.ValueReceived;
                try
                {
                    LuaInterface.LoggingInterface.Log(string.Format(
                                                          "IsDoStringHasValue() - State: {0}:{1}", lstate, vstate));

                    return ((lstate == 255) && vstate);
                }
                catch (Exception e)
                {
                    LuaInterface.LoggingInterface.Log("IsDoStringHasValue() - Exception: " + e.ToString());
                    return true;
                }
            }
        }

        public bool SetEndSceneState(byte NewState)
        {
            lock (LuaInterface.dataLock)
            {
                byte OldState = LuaInterface.LuaState;
                LuaInterface.LoggingInterface.Log(string.Format(
                                                      "SetEndSceneState() - Old: {0}; New: {1}", OldState, NewState));
                if (OldState == 255)
                {
                    LuaInterface.LoggingInterface.Log("SetEndSceneState() - Accepted");
                    LuaInterface.LuaState = NewState;
                    return true;
                }
                else
                {
                    LuaInterface.LoggingInterface.Log("SetEndSceneState() - Rejected");
                    return false;
                }
            }
        }

        public void SetPatchOffset(uint poffset)
        {
            LuaInterface.PatchOffset = poffset;
        }

        public void RegisterLuaDelegate(Dictionary<string, uint> lua_config)
        {
            LuaInterface.RegisterLuaDelegate(lua_config);
        }

        public void SetWowMainThread(int threadId)
        {
            LuaInterface.WowMainThreadId = threadId;
        }
    }
}
