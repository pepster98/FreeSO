﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FSO.SimAntics.NetPlay.Model.Commands
{
    class VMNetSimLeaveCmd : VMNetCommandBodyAbstract
    {
        public uint SimID;

        public override bool Execute(VM vm)
        {
            var sim = vm.Entities.FirstOrDefault(x => x is VMAvatar && x.PersistID == SimID);

            if (sim != null)
            {
                sim.Delete(true, vm.Context);
                vm.SignalChatEvent(new VMChatEvent(sim.PersistID, VMChatEventType.Leave, ((VMAvatar)sim).Name));
            }
            return true;
        }

        #region VMSerializable Members
        public override void SerializeInto(BinaryWriter writer)
        {
            writer.Write(SimID);
        }

        public override void Deserialize(BinaryReader reader)
        {
            SimID = reader.ReadUInt32();
        }
        #endregion
    }
}
