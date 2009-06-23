﻿using System.Collections.Generic;

namespace Dante
{
    public interface ISharedAssembly
    {
        void DoString(string command);
        void DoStringInputHandler(string command);
        List<string> GetValues();
        void Patch();
        void RestorePatch();
    }
}
