﻿/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using Charvatia.Properties;
using FSO.SimAntics;
using FSO.SimAntics.NetPlay;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Charvatia
{
    public class CVMInstance
    {
        public VM state;
        private Stopwatch timeKeeper;

        public CVMInstance(int port)
        {
            VM.UseWorld = false;
            VMNetDriver driver;
            driver = new VMServerDriver(port);

            var vm = new VM(new VMContext(null), driver);
            vm.Init();

            vm.SendCommand(new VMBlueprintRestoreCmd
            {
                XMLData = File.ReadAllBytes(Path.Combine(Settings.Default.GamePath+"housedata/blueprints/"+Settings.Default.DebugLot))
            });
            vm.Context.Clock.Hours = 10;

            state = vm;
        }

        public void Start()
        {
            Thread oThread = new Thread(new ThreadStart(TickVM));
            oThread.Start();
        }

        private void TickVM()
        {
            timeKeeper = new Stopwatch();
            timeKeeper.Start();
            long lastMs = 0;
            while (true)
            {
                lastMs += 16;
                state.Update();
                Thread.Sleep((int)Math.Max(0, (lastMs + 16)-timeKeeper.ElapsedMilliseconds));
            }
        }
    }
}
