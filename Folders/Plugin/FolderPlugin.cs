﻿using SuchByte.MacroDeck.Folders.Plugin.GUI;
using SuchByte.MacroDeck.GUI;
using SuchByte.MacroDeck.GUI.CustomControls;
using SuchByte.MacroDeck.Language;
using SuchByte.MacroDeck.Plugins;
using SuchByte.MacroDeck.Server;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace SuchByte.MacroDeck.Folders.Plugin
{
    public class FolderPlugin : MacroDeckPlugin
    {
        public override string Name => LanguageManager.Strings.PluginMacroDeckFolder;
        public override string Author => "Macro Deck";
        public override void Enable()
        {
            this.Actions = new List<PluginAction>
            {
                new FolderSwitcher(),
                new GoToParentFolder(),
                new GoToRootFolder(),
            };
        }
    }

    public class FolderSwitcher : PluginAction
    {
        public override string Name => LanguageManager.Strings.ActionChangeFolder;
        public override bool CanConfigure => true;
        public override string Description => LanguageManager.Strings.ActionChangeFolderDescription;

        public override void Trigger(string clientId, ActionButton.ActionButton actionButton)
        {
            MacroDeckServer.SetFolder(MacroDeckServer.GetMacroDeckClient(clientId), MacroDeck.ProfileManager.FindFolderById(long.Parse(this.Configuration), MacroDeckServer.GetMacroDeckClient(clientId).Profile));
        }
        public override ActionConfigControl GetActionConfigControl(ActionConfigurator actionConfigurator)
        {
            return new FolderSwitcherConfigurator(this);
        }
    }

    public class GoToParentFolder : PluginAction
    {
        public override string Name => LanguageManager.Strings.ActionGoToParentFolder;
        public override string Description => LanguageManager.Strings.ActionGoToParentFolderDescription;
        public override void Trigger(string clientId, ActionButton.ActionButton actionButton)
        {
            try
            {
                MacroDeckClient macroDeckClient = MacroDeckServer.GetMacroDeckClient(clientId);
                MacroDeckFolder parentFolder = MacroDeck.ProfileManager.FindParentFolder(macroDeckClient.Folder, macroDeckClient.Profile);
                MacroDeckServer.SetFolder(macroDeckClient, parentFolder);
            } catch { }
        }
        public override ActionConfigControl GetActionConfigControl(ActionConfigurator actionConfigurator)
        {
            return null;
        }
    }

    public class GoToRootFolder : PluginAction
    {
        public override string Name => LanguageManager.Strings.ActionGoToRootFolder;
        public override string Description => LanguageManager.Strings.ActionGoToRootFolderDescription;
        public override void Trigger(string clientId, ActionButton.ActionButton actionButton)
        {
            try
            {
                MacroDeckClient macroDeckClient = MacroDeckServer.GetMacroDeckClient(clientId);
                MacroDeckFolder rootFolder = macroDeckClient.Profile.Folders.Find(folder => folder.FolderId == 0);
                MacroDeckServer.SetFolder(macroDeckClient, rootFolder);
            }
            catch { }
        }
        public override ActionConfigControl GetActionConfigControl(ActionConfigurator actionConfigurator)
        {
            return null;
        }
    }
}