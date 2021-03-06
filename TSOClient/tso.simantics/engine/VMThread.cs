﻿/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/. 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics.Primitives;
using FSO.SimAntics.Model;
using FSO.SimAntics.Marshals.Threads;

namespace FSO.SimAntics.Engine
{
    /// <summary>
    /// Handles instruction execution
    /// </summary>
    public class VMThread
    {
        public VMContext Context;
        private VMEntity Entity;

        public VMThreadBreakMode ThreadBreak = VMThreadBreakMode.Active;
        public int BreakFrame; //frame the last breakpoint was performed on
        public bool RoutineDirty;

        //check tree only vars
        public bool IsCheck;
        public List<VMPieMenuInteraction> ActionStrings;

        public List<VMStackFrame> Stack;
        private bool ContinueExecution;
        public List<VMQueuedAction> Queue;
        public short[] TempRegisters = new short[20];
        public int[] TempXL = new int[2];
        public VMPrimitiveExitCode LastStackExitCode = VMPrimitiveExitCode.GOTO_FALSE;

        public VMDialogResult BlockingDialog; 
        public bool Interrupt;

        private ushort ActionUID;
        public int DialogCooldown = 0;

        public static VMPrimitiveExitCode EvaluateCheck(VMContext context, VMEntity entity, VMQueuedAction action)
        {
            return EvaluateCheck(context, entity, action, null);
        }
        public static VMPrimitiveExitCode EvaluateCheck(VMContext context, VMEntity entity, VMQueuedAction action, List<VMPieMenuInteraction> actionStrings)
        {
            var temp = new VMThread(context, entity, 5);
            if (entity.Thread != null)
            {
                temp.TempRegisters = entity.Thread.TempRegisters;
                temp.TempXL = entity.Thread.TempXL;
            }
            temp.IsCheck = true;
            temp.ActionStrings = actionStrings; //generate and place action strings in here
            temp.EnqueueAction(action);
            while (temp.Queue.Count > 0 && temp.DialogCooldown == 0) //keep going till we're done! idling is for losers!
            {
                temp.Tick();
                temp.ThreadBreak = VMThreadBreakMode.Active; //cannot breakpoint in check trees
            }
            return (temp.DialogCooldown > 0) ? VMPrimitiveExitCode.ERROR:temp.LastStackExitCode;
        }

        public bool RunInMyStack(BHAV bhav, GameObject CodeOwner, short[] passVars, VMEntity stackObj)
        {
            var OldStack = Stack;
            var OldQueue = Queue;
            var OldCheck = IsCheck;

            VMStackFrame prevFrame = new VMStackFrame() { Caller = Entity, Callee = Entity };
            if (Stack.Count > 0)
            {
                prevFrame = Stack[Stack.Count - 1];
                Stack = new List<VMStackFrame>() { prevFrame };
            } else
            {
                Stack = new List<VMStackFrame>();
            }

            if (Queue.Count > 0)
            {
                Queue = new List<VMQueuedAction>() { Queue[0] };
            } else
            {
                Queue = new List<VMQueuedAction>();
            }
            IsCheck = true;

            ExecuteSubRoutine(prevFrame, bhav, CodeOwner, new VMSubRoutineOperand(passVars));
            Stack.RemoveAt(0);
            if (Stack.Count == 0)
            {
                Stack = OldStack;
                Queue = OldQueue;
                return false;
                //bhav was invalid/empty
            }
            var frame = Stack[Stack.Count - 1];
            frame.StackObject = stackObj;

            try {
                while (Stack.Count > 0)
                {
                    NextInstruction();
                }
            } catch (Exception)
            {
                //we need to catch these so that the parent can be restored.
            }

            //copy child stack things to parent stack
            Stack = OldStack;
            Queue = OldQueue;
            IsCheck = OldCheck;

            return (LastStackExitCode == VMPrimitiveExitCode.RETURN_TRUE) ? true : false;
        }

        public VMThread(VMContext context, VMEntity entity, int stackSize){
            this.Context = context;
            this.Entity = entity;

            this.Stack = new List<VMStackFrame>(stackSize);
            this.Queue = new List<VMQueuedAction>();
        }

        public void Tick(){
            if (ThreadBreak == VMThreadBreakMode.Pause) return;
            else if (ThreadBreak == VMThreadBreakMode.Immediate)
            {
                Breakpoint(Stack.LastOrDefault()); return;
            }
            if (RoutineDirty)
            {
                foreach (var frame in Stack)
                    if (frame.Routine.Chunk.RuntimeVer != frame.Routine.RuntimeVer) frame.Routine = Context.VM.Assemble(frame.Routine.Chunk); 
                RoutineDirty = false;
            }

            if (DialogCooldown > 0) DialogCooldown--;
//#if !DEBUG
            try {
                //#endif
                if (!Entity.Dead)
                {
                    EvaluateQueuePriorities();
                    if (Stack.Count == 0)
                    {
                        if (Queue.Count == 0)
                        {
                            //todo: should restart main
                            return;
                        }
                        var item = Queue[0];
                        if (item.Cancelled) Entity.SetFlag(VMEntityFlags.InteractionCanceled, true);
                        if (IsCheck || (item.Mode != VMQueueMode.ParentIdle || !Entity.GetFlag(VMEntityFlags.InteractionCanceled)))
                            ExecuteAction(item);
                        else {
                            Queue.RemoveAt(0);
                            return;
                        }
                    }
                    if (!Queue[0].Callee.Dead)
                    {
                        if (ThreadBreak == VMThreadBreakMode.ReturnTrue)
                        {
                            var bf = Stack[BreakFrame];
                            HandleResult(bf, bf.GetCurrentInstruction(), VMPrimitiveExitCode.RETURN_TRUE);
                            Breakpoint(Stack.LastOrDefault());
                            return;
                        }
                        if (ThreadBreak == VMThreadBreakMode.ReturnFalse)
                        {
                            var bf = Stack[BreakFrame];
                            HandleResult(bf, bf.GetCurrentInstruction(), VMPrimitiveExitCode.RETURN_TRUE);
                            Breakpoint(Stack.LastOrDefault());
                            return;
                        }
                        ContinueExecution = true;
                        var interaction = Queue[0];
                        while (ContinueExecution)
                        {
                            ContinueExecution = false;
                            NextInstruction();
                        }

                        //clear "interaction cancelled" if we're going into the next action.
                        if (Stack.Count == 0 && interaction.Mode != VMQueueMode.ParentIdle) Entity.SetFlag(VMEntityFlags.InteractionCanceled, false);
                    }
                    else //interaction owner is dead, rip
                    {
                        Stack.Clear();
                        if (Queue[0].Callback != null) Queue[0].Callback.Run(Entity);
                        if (Queue.Count > 0) Queue.RemoveAt(0);
                    }
                }
                else
                {
                    Queue.Clear();
                }

//#if !DEBUG
            } catch (Exception e) {
                var context = Stack[Stack.Count - 1];
                bool Delete = ((Entity is VMGameObject) && (DialogCooldown > 30 * 20 - 10));
                if (DialogCooldown == 0)
                {
                    
                    var simExcept = new VMSimanticsException(e.Message, context);
                    string exceptionStr = "A SimAntics Exception has occurred, and has been suppressed: \r\n\r\n" + simExcept.ToString() + "\r\n\r\nThe object will be reset. Please report this!";
                    VMDialogInfo info = new VMDialogInfo
                    {
                        Caller = null,
                        Icon = context.Callee,
                        Operand = new VMDialogOperand { },
                        Message = exceptionStr,
                        Title = "SimAntics Exception!"
                    };
                    Context.VM.SignalDialog(info);
                    DialogCooldown = 30 * 20;
                }

                context.Callee.Reset(context.VM.Context);
                context.Caller.Reset(context.VM.Context);

                if (Delete) Entity.Delete(true, context.VM.Context);
            }
//#endif
            //Interrupt = true;
        }

        private void EvaluateQueuePriorities() {
            if (Queue.Count == 0) return;
            int CurrentPriority = (int)Queue[0].Priority;
            for (int i = 1; i < Queue.Count; i++)
            {
                if ((int)Queue[i].Priority > CurrentPriority)
                {
                    Queue[0].Cancelled = true;
                    Entity.SetFlag(VMEntityFlags.InteractionCanceled, true);
                    break;
                }
            }
        }

        private void NextInstruction()
        {
            if (Stack.Count == 0){
                return;
            }

            /** Next instruction **/
            var currentFrame = Stack.Last();

            if (currentFrame is VMRoutingFrame) HandleResult(currentFrame, null, ((VMRoutingFrame)currentFrame).Tick());
            else ExecuteInstruction(currentFrame);
        }

        public VMRoutingFrame PushNewRoutingFrame(VMStackFrame frame, bool failureTrees)
        {
            var childFrame = new VMRoutingFrame
            {
                Routine = frame.Routine,
                Caller = frame.Caller,
                Callee = frame.Callee,
                CodeOwner = frame.CodeOwner,
                StackObject = frame.StackObject,
                Thread = this,
                CallFailureTrees = failureTrees
            };

            Stack.Add(childFrame);
            return childFrame;
        }

        public void ExecuteSubRoutine(VMStackFrame frame, BHAV bhav, GameObject codeOwner, VMSubRoutineOperand args)
        {
            if (bhav == null){
                Pop(VMPrimitiveExitCode.ERROR);
                return;
            }

            var routine = Context.VM.Assemble(bhav);
            var childFrame = new VMStackFrame
            {
                Routine = routine,
                Caller = frame.Caller,
                Callee = frame.Callee,
                CodeOwner = codeOwner,
                StackObject = frame.StackObject
            };
            childFrame.Args = new short[(routine.Arguments>4)?routine.Arguments:4];
            for (var i = 0; i < childFrame.Args.Length; i++){
                short argValue = (i>3)?(short)-1:args.Arguments[i];
                if (argValue == -1)
                {
                    argValue = TempRegisters[i];
                }
                childFrame.Args[i] = argValue;
            }
            Push(childFrame);
        }

        private void ExecuteInstruction(VMStackFrame frame){
            var instruction = frame.GetCurrentInstruction();
            var opcode = instruction.Opcode;

            if (opcode >= 256)
            {
                BHAV bhav = null;

                GameObject CodeOwner;
                if (opcode >= 8192)
                {
                    // Semi-Global sub-routine call
                    bhav = frame.ScopeResource.SemiGlobal.Get<BHAV>(opcode);
                }
                else if (opcode >= 4096)
                {
                    // Private sub-routine call
                    bhav = frame.ScopeResource.Get<BHAV>(opcode);
                }
                else
                {
                    // Global sub-routine call
                    //CodeOwner = frame.Global.Resource;
                    bhav = frame.Global.Resource.Get<BHAV>(opcode);
                }

                CodeOwner = frame.CodeOwner;

                var operand = (VMSubRoutineOperand)instruction.Operand;
                ExecuteSubRoutine(frame, bhav, CodeOwner, operand);
                if (Stack.LastOrDefault().GetCurrentInstruction().Breakpoint || ThreadBreak == VMThreadBreakMode.StepIn)
                {
                    Breakpoint(frame);
                    ContinueExecution = false;
                } else
                {
                    ContinueExecution = true;
                }
                return;
            }
            

            var primitive = Context.Primitives[opcode];
            if (primitive == null)
            {
                //throw new Exception("Unknown primitive!");
                HandleResult(frame, instruction, VMPrimitiveExitCode.GOTO_TRUE);
                return;
                //Pop(VMPrimitiveExitCode.ERROR);
                
            }

            VMPrimitiveHandler handler = primitive.GetHandler();
            var result = handler.Execute(frame, instruction.Operand);
            HandleResult(frame, instruction, result);
        }

        private void HandleResult(VMStackFrame frame, VMInstruction instruction, VMPrimitiveExitCode result)
        {
            switch (result)
            {
                // Don't advance the instruction pointer, this primitive isnt finished yet
                case VMPrimitiveExitCode.CONTINUE_NEXT_TICK:
                    ContinueExecution = false;
                    break;
                case VMPrimitiveExitCode.ERROR:
                    Pop(result);
                    break;
                case VMPrimitiveExitCode.RETURN_TRUE:
                case VMPrimitiveExitCode.RETURN_FALSE:
                    /** pop stack and return false **/
                    Pop(result);
                    break;
                case VMPrimitiveExitCode.GOTO_TRUE:
                    MoveToInstruction(frame, instruction.TruePointer, true);
                    break;
                case VMPrimitiveExitCode.GOTO_FALSE:
                    MoveToInstruction(frame, instruction.FalsePointer, true);
                    break;
                case VMPrimitiveExitCode.GOTO_TRUE_NEXT_TICK:
                    MoveToInstruction(frame, instruction.TruePointer, false);
                    break;
                case VMPrimitiveExitCode.GOTO_FALSE_NEXT_TICK:
                    MoveToInstruction(frame, instruction.FalsePointer, false);
                    break;
                case VMPrimitiveExitCode.CONTINUE:
                    ContinueExecution = true;
                    break;
                case VMPrimitiveExitCode.INTERRUPT:
                    Stack.Clear();
                    if (Queue.Count > 0) Queue.RemoveAt(0);
                    LastStackExitCode = result;
                    break;
            }
        }

        private void MoveToInstruction(VMStackFrame frame, byte instruction, bool continueExecution){
            if (frame is VMRoutingFrame)
            {
                //TODO: Handle returning false into the pathfinder (indicates failure)
                return;
            }

            switch (instruction) {
                case 255:
                    Pop(VMPrimitiveExitCode.RETURN_FALSE);
                    break;
                case 254:
                    Pop(VMPrimitiveExitCode.RETURN_TRUE); break;
                case 253:
                    Pop(VMPrimitiveExitCode.ERROR); break;
                default:
                    frame.InstructionPointer = instruction;
                    if (frame.GetCurrentInstruction().Breakpoint || 
                        (ThreadBreak != VMThreadBreakMode.Active && (
                            ThreadBreak == VMThreadBreakMode.StepIn || 
                            (ThreadBreak == VMThreadBreakMode.StepOver && Stack.Count-1 <= BreakFrame) ||
                            (ThreadBreak == VMThreadBreakMode.StepOut && Stack.Count <= BreakFrame)
                        )))
                    {
                        Breakpoint(frame);
                    }
                    break;
            }

            ContinueExecution = (ThreadBreak != VMThreadBreakMode.Pause) && continueExecution;
        }

        public void Breakpoint(VMStackFrame frame)
        {
            if (IsCheck) return; //can't breakpoint in check trees.
            ThreadBreak = VMThreadBreakMode.Pause;
            BreakFrame = Stack.IndexOf(frame);
            Context.VM.BreakpointHit(Entity);
        }

        private void ExecuteAction(VMQueuedAction action){
            var frame = new VMStackFrame {
                Caller = Entity,
                Callee = action.Callee,
                CodeOwner = action.CodeOwner,
                Routine = action.Routine,
                StackObject = action.StackObject
            };
            if (action.Args == null) frame.Args = new short[4]; //always 4? i got crashes when i used the value provided by the routine, when for that same routine edith displayed 4 in the properties...
            else frame.Args = action.Args; //WARNING - if you use this, the args array MUST have the same number of elements the routine is expecting!
            
            Push(frame);
        }

        public void Pop(VMPrimitiveExitCode result){
            Stack.RemoveAt(Stack.Count - 1);
            LastStackExitCode = result;

            if (Stack.Count > 0)
            {
                if (result == VMPrimitiveExitCode.RETURN_TRUE)
                {
                    result = VMPrimitiveExitCode.GOTO_TRUE;
                }
                if (result == VMPrimitiveExitCode.RETURN_FALSE)
                {
                    result = VMPrimitiveExitCode.GOTO_FALSE;
                }

                var currentFrame = Stack.Last();
                HandleResult(currentFrame, currentFrame.GetCurrentInstruction(), result);
            }
            else //interaction finished!
            {
                if (Queue[0].Callback != null) Queue[0].Callback.Run(Entity);
                if (Queue.Count > 0) Queue.RemoveAt(0);
            }
        }

        public void Push(VMStackFrame frame)
        {
            if (frame.Routine.Instructions.Length == 0) return; //some bhavs are empty... do not execute these.
            Stack.Add(frame);

            /** Initialize the locals **/
            var numLocals = Math.Max(frame.Routine.Locals, frame.Routine.Arguments);
            frame.Locals = new short[numLocals];
            frame.Thread = this;

            frame.InstructionPointer = 0;
        }

        /// <summary>
        /// Add an item to the action queue
        /// </summary>
        /// <param name="invocation"></param>
        public void EnqueueAction(VMQueuedAction invocation)
        {
            if (!IsCheck && (invocation.Flags & TTABFlags.RunImmediately) > 0)
            {
                EvaluateCheck(Context, Entity, invocation);
                return;
            }

            invocation.UID = ActionUID++;
            if (Queue.Count == 0) //if empty, just queue right at the front 
                this.Queue.Add(invocation);
            else if ((invocation.Flags & TTABFlags.Leapfrog) > 0)
                //place right after active interaction, ignoring all priorities.
                this.Queue.Insert(1, invocation);
            else //we've got an even harder job! find a place for this interaction based on its priority
            {
                bool hitParentEnd = (invocation.Mode != VMQueueMode.ParentIdle);
                for (int i = Queue.Count - 1; i > 0; i--)
                {
                    if (hitParentEnd && (invocation.Priority <= Queue[i].Priority || Queue[i].Mode == VMQueueMode.ParentExit)) //skip until we find a parent exit or something with the same or higher priority.
                    {
                        this.Queue.Insert(i+1, invocation);
                        EvaluateQueuePriorities();
                        return;
                    }
                    if (Queue[i].Mode == VMQueueMode.ParentExit) hitParentEnd = true;
                }
                this.Queue.Insert(1, invocation); //this is more important than all other queued items that are not running, so stick this to run next.
            }
            EvaluateQueuePriorities();
        }

        #region VM Marshalling Functions
        public virtual VMThreadMarshal Save()
        {
            var stack = new VMStackFrameMarshal[Stack.Count];
            int i = 0;
            foreach (var item in Stack) stack[i++] = item.Save();

            var queue = new VMQueuedActionMarshal[Queue.Count];
            i = 0;
            foreach (var item in Queue) queue[i++] = item.Save();

            return new VMThreadMarshal
            {
                Stack = stack,
                Queue = queue,
                TempRegisters = TempRegisters,
                TempXL = TempXL,
                LastStackExitCode = LastStackExitCode,

                BlockingDialog = BlockingDialog,

                Interrupt = Interrupt,

                ActionUID = ActionUID,
                DialogCooldown = DialogCooldown
            };
        }

        public virtual void Load(VMThreadMarshal input, VMContext context)
        {
            Stack = new List<VMStackFrame>();
            foreach (var item in input.Stack)
            {
                Stack.Add((item is VMRoutingFrameMarshal)? new VMRoutingFrame(item, context, this) : new VMStackFrame(item, context, this));
            }
            Queue = new List<VMQueuedAction>();
            foreach (var item in input.Queue) Queue.Add(new VMQueuedAction(item, context));
            TempRegisters = input.TempRegisters;
            TempXL = input.TempXL;
            LastStackExitCode = input.LastStackExitCode;

            BlockingDialog = input.BlockingDialog;
            Interrupt = input.Interrupt;
            ActionUID = input.ActionUID;
            DialogCooldown = input.DialogCooldown;
        }

        public VMThread(VMThreadMarshal input, VMContext context, VMEntity entity)
        {
            Context = context;
            Entity = entity;
            Load(input, context);
        }
        #endregion
    }

    public enum VMThreadBreakMode
    {
        Active = 0,
        Pause = 1,
        StepIn = 2,
        StepOut = 3,
        StepOver = 4,
        ReturnTrue = 5,
        ReturnFalse = 6,
        Immediate = 7
    }
}
