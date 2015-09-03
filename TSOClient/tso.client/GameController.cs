﻿/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FSO.Client.UI.Screens;
using FSO.Client.Network;
using ProtocolAbstractionLibraryD;
using FSO.Client.UI.Framework;
using FSO.Client.GameContent;
using Ninject;
using FSO.Server.Protocol.CitySelector;
using FSO.Client.Controllers;
using FSO.Common.Utils;

namespace FSO.Client
{
    /// <summary>
    /// Handles the game flow between various game modes, e.g. login => city view
    /// </summary>
    public class GameController
    {
        private object CurrentController;
        private UIScreen CurrentView;
        private IKernel Kernel;

        public GameController(IKernel kernel)
        {
            this.Kernel = kernel;
        }

        public void DebugShowTypeFaceScreen()
        {
            var screen = new DebugTypeFaceScreen();

            /** Remove preload screen **/
            GameFacade.Screens.AddScreen(screen);
        }

        /// <summary>
        /// Start the preloading process
        /// </summary>
        public void StartLoading()
        {
            UIScreen screen;
            if (GlobalSettings.Default.SkipIntro)
            {
                screen = Kernel.Get<LoadingScreen>();
            }
            else
            {
                screen = Kernel.Get<EALogo>();
            }
            GameFacade.Screens.AddScreen(screen);
            ContentManager.InitLoading();
        }

        /// <summary>
        /// Show the login screen
        /// </summary>
        public void ShowLogin()
        {
            var screen = Kernel.Get<LoginScreen>();

            /** Remove preload screen **/
            GameFacade.Screens.RemoveCurrent();
            GameFacade.Screens.AddScreen(screen);
        }

        /// <summary>
        /// Go to the person selection page
        /// </summary>
        public void ShowPersonSelection()
        {
            ChangeState<PersonSelection, PersonSelectionController>((view, controller) =>
            {
            });
        }

        public void ShowPersonCreation(ShardStatusItem selectedCity)
        {
            var screen = Kernel.Get<PersonSelectionEdit>();
            //screen.SelectedCity = selectedCity;
            GameFacade.Screens.RemoveCurrent();
            GameFacade.Screens.AddScreen(screen);
        }


        public void ConnectToCAS(string cityName)
        {
            /**
             * Steps:
             *  1) Show transition screen and open a connectino to the server
             *  2) If connection succeeds, go to CAS
             *  3) If connection fails, go back to SAS
             */
            ChangeState<TransitionScreen, ConnectCASController>((view, controller) =>
            {
                controller.Connect(cityName, new Common.Utils.Callback(GotoCAS), new Common.Utils.Callback(Disconnect));
            });
        }

        public void GotoCAS(){
            ChangeState<PersonSelectionEdit, PersonSelectionEditController>((view, controller) => {
            });
        }

        public void Disconnect(){
            ChangeState<TransitionScreen, DisconnectController>((view, controller) =>
            {
                controller.Disconnect(HandleDisconnect);
            });
        }

        private void HandleDisconnect(){
            //Depending on how long is left on the session take user
            //to SAS or login screen
            ShowPersonSelection();
        }


        private void ChangeState<TView, TController>(Callback<TView, TController> onCreated) where TView : UIScreen
        {
            GameFacade.Screens.OnNextUpdate(x =>
            {
                if (CurrentController != null)
                {
                    if (CurrentController is IDisposable)
                    {
                        ((IDisposable)CurrentController).Dispose();
                    }
                }

                var view = (UIScreen)Kernel.Get<TView>();
                var controller = view.BindController<TController>();
                GameFacade.Screens.RemoveCurrent();
                GameFacade.Screens.AddScreen(view);

                CurrentController = controller;
                CurrentView = view;

                onCreated((TView)view, controller);
            });
        }





        public void ShowCity()
        {
            var screen = Kernel.Get<CoreGameScreen>();
            GameFacade.Screens.RemoveCurrent();
            GameFacade.Screens.AddScreen(screen);
        }

        public void ShowCredits()
        {
            var screen = Kernel.Get<Credits>();
            GameFacade.Screens.RemoveCurrent();
            GameFacade.Screens.AddScreen(screen);
        }

        public void ShowLotDebug()
        {
            var screen = Kernel.Get<CoreGameScreen>(); //new LotDebugScreen();
            GameFacade.Screens.RemoveCurrent();
            GameFacade.Screens.AddScreen(screen);
            //screen.InitTestLot();
            //screen.ZoomLevel = 1;
        }

        public void StartDebugTools()
        {
            if (GameFacade.DebugWindow != null)
            {
                if (GameFacade.DebugWindow.Visible)
                {
                    GameFacade.DebugWindow.Hide();
                }
                else
                {
                    GameFacade.DebugWindow.Show();
                }
                return;
            }

            var debugWindow = new FSO.Client.Debug.TSOClientTools();
            GameFacade.DebugWindow = debugWindow;

            /** Position the debug window **/

            debugWindow.Show();

            //debugWindow.PositionAroundGame(GameFacade.Game.Window);
        }
    }

}