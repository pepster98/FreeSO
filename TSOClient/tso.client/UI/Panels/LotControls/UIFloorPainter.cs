﻿/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FSO.LotView;
using FSO.LotView.Components;
using FSO.LotView.Model;
using FSO.Common.Rendering.Framework.Model;
using FSO.HIT;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Model.Commands;
using FSO.SimAntics.Utils;
using FSO.Client.UI.Model;

namespace FSO.Client.UI.Panels.LotControls
{
    public class UIFloorPainter : UICustomLotControl
    {

        VM vm;
        LotView.World World;
        UILotControl Parent;

        bool Drawing;
        private List<VMArchitectureCommand> Commands;
        int CursorDir = 0;

        int StartX = -1;
        int StartY = -1;

        ushort Pattern;

        public UIFloorPainter (VM vm, LotView.World world, UILotControl parent, List<int> parameters)
        {
            Pattern = (ushort)parameters[0];

            this.vm = vm;
            World = parent.World;
            Parent = parent;

            Commands = new List<VMArchitectureCommand>();

        }


        public void MouseDown(UpdateState state)
        {
            HITVM.Get().PlaySoundEvent(UISounds.BuildDragToolDown);
            Drawing = true;
        }

        public void MouseUp(UpdateState state)
        {
            HITVM.Get().PlaySoundEvent(UISounds.BuildDragToolUp);

            vm.SendCommand(new VMNetArchitectureCmd
            {
                Commands = new List<VMArchitectureCommand>(Commands)
            });

            Commands.Clear();
            Drawing = false;
        }

        public void Release()
        {
        }

        public void Update(UpdateState state, bool scrolled)
        {
            ushort pattern = (state.KeyboardState.IsKeyDown(Keys.LeftControl)) ? (ushort)0 : Pattern;

            var tilePos = World.State.WorldSpace.GetTileAtPosWithScroll(new Vector2(state.MouseState.X, state.MouseState.Y));
            Point cursor = new Point((int)tilePos.X, (int)tilePos.Y);

            if (!Drawing && Commands.Count > 0)
            {
                vm.Context.Architecture.SignalRedraw();
                Commands.Clear();
            }
            if (state.KeyboardState.IsKeyDown(Keys.LeftShift) && pattern < 65534)
            {
                if (Commands.Count == 0 || Commands[0].Type != VMArchitectureCommandType.FLOOR_FILL)
                {
                    Commands.Clear();
                    vm.Context.Architecture.SignalRedraw();
                }

                Commands.Add(new VMArchitectureCommand
                {
                    Type = VMArchitectureCommandType.FLOOR_FILL,
                    level = World.State.Level,
                    pattern = pattern,
                    style = 0,
                    x = cursor.X,
                    y = cursor.Y,
                });
            } else
            {
                if (Commands.Count > 0 && Commands[0].Type == VMArchitectureCommandType.FLOOR_FILL)
                {
                    Commands.Clear();
                    vm.Context.Architecture.SignalRedraw();
                }

                if (!Drawing || Commands.Count == 0)
                {
                    StartX = cursor.X;
                    StartY = cursor.Y;
                }
                
                int dir = 0;
                Vector2 fract = new Vector2(tilePos.X - cursor.X, tilePos.Y - cursor.Y);
                if (fract.X-fract.Y > 0)
                {
                    dir = (fract.X + fract.Y > 1) ? 2 : 1;
                } else
                {
                    dir = (fract.X + fract.Y > 1) ? 3 : 0;
                }

                int smallX = Math.Min(StartX, cursor.X);
                int smallY = Math.Min(StartY, cursor.Y);
                int bigX = Math.Max(StartX, cursor.X);
                int bigY = Math.Max(StartY, cursor.Y);

                var cmd = new VMArchitectureCommand
                {
                    Type = VMArchitectureCommandType.FLOOR_RECT,
                    level = World.State.Level,
                    pattern = pattern,
                    style = (ushort)dir,
                    x = smallX,
                    y = smallY,
                    x2 = bigX-smallX,
                    y2 = bigY-smallY
                };
                if (!Commands.Contains(cmd))
                {
                    Commands.Clear();
                    vm.Context.Architecture.SignalRedraw();
                    Commands.Add(cmd);
                }
            }

            var cmds = vm.Context.Architecture.Commands;
            cmds.Clear();
            foreach (var cmd in Commands)
            {
                cmds.Add(cmd);
            }
        }
    }
}
