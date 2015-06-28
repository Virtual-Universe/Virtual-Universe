﻿/*
 * Copyright (c) Contributors, http://virtual-planets.org/, http://whitecore-sim.org/, http://aurora-sim.org/, http://opensimulator.org
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Virtual-Universe Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Universe.Framework.ConsoleFramework;
using Universe.Framework.Modules;
using Universe.Framework.PresenceInfo;
using Universe.Framework.SceneInfo;
using Universe.Framework.Services;
using Universe.Framework.Utilities;

namespace Universe.Modules.Currency
{
    public class BaseCurrencyServiceModule : IMoneyModule, IService
    {
        IUserAccountService m_accountService;
        IBaseCurrencyConnector m_Connector;

        string bankerName = Constants.BankerName;
        string marketplaceName = Constants.MarketplaceName;

        #region Declares

        BaseCurrencyConfig Config
        {
            get { return m_connector.GetConfig(); }
        }
        List<IScene> m_scenes = new List<IScene>();
        BaseCurrencyConnector m_connector;
        IRegistryCore m_registry;

        #endregion

        #region IService Members

        public UUID BankerUUID
        {
            get { return (UUID)Constants.BankerUUID; }
        }

        public string BankerName
        {
            get { return bankerName; }
        }

        public UUID MarketplaceUUID
        {
            get { return (UUID)Constants.MarketplaceUUID; }
        }

        public string MarketplaceName
        {
            get { return marketplaceName; }
        }

        public void Initialize(IConfigSource config, IRegistryCore registry)
        {
            if (Config != null)
            {
                bankerName = Config.GetString("BankerName", bankerName);
                marketplaceName = Config.GetString("MarketplaceName", marketplaceName);
            }

            if (config.Configs["Currency"] == null ||
                config.Configs["Currency"].GetString("Module", "") != "BaseCurrency")
                return;

            m_registry = registry;
            m_connector = Framework.Utilities.DataManager.RequestPlugin<IBaseCurrencyConnector>() as BaseCurrencyConnector;
        }

        public void Start(IConfigSource config, IRegistryCore registry)
        {
            if (m_registry == null)
                return;
            ISyncMessageRecievedService syncRecievedService =
                registry.RequestModuleInterface<ISyncMessageRecievedService>();
            if (syncRecievedService != null)
                syncRecievedService.OnMessageReceived += syncRecievedService_OnMessageReceived;
        }

        public void FinishedStartup()
        {
            m_accountService = m_registry.RequestModuleInterface<IUserAccountService>();
            m_Connector = Framework.Utilities.DataManager.RequestPlugin<IBaseCurrencyConnector>();

            // these are only valid if we are local
            if (!m_accountService.RemoteCalls())
            {
                // check and/or create default banker and marketplace user
                CheckBankerUserInfo();
                CheckMarketplaceUserInfo();

                AddCommands();
            }

            if (m_registry == null)
                return;

            m_registry.RegisterModuleInterface<IMoneyModule>(this);

            ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager>();
            if (manager != null)
            {
                manager.OnAddedScene += (scene) =>
                {
                                                m_scenes.Add(scene);
                                                scene.EventManager.OnNewClient += OnNewClient;
                                                scene.EventManager.OnClosingClient += OnClosingClient;
                                                scene.EventManager.OnMakeRootAgent += OnMakeRootAgent;
                                                scene.EventManager.OnValidateBuyLand += EventManager_OnValidateBuyLand;
                                                scene.RegisterModuleInterface<IMoneyModule>(this);
                                            };
                manager.OnCloseScene += (scene) =>
                                            {
                                                scene.EventManager.OnNewClient -= OnNewClient;
                                                scene.EventManager.OnClosingClient -= OnClosingClient;
                                                scene.EventManager.OnMakeRootAgent -= OnMakeRootAgent;
                                                scene.EventManager.OnValidateBuyLand -= EventManager_OnValidateBuyLand;
                                                scene.RegisterModuleInterface<IMoneyModule>(this);
                                                m_scenes.Remove(scene);
                                            };
            }


            if (!m_connector.DoRemoteCalls)
            {
                if ((m_connector.GetConfig().GiveStipends) && (m_connector.GetConfig().Stipend > 0))
                    new GiveStipends(m_connector.GetConfig(), m_registry, m_connector);
            }
        }

        private void AddCommands()
        {
            throw new NotImplementedException();
        }

        bool EventManager_OnValidateBuyLand(EventManager.LandBuyArgs e)
        {
            IParcelManagementModule parcelManagement = GetSceneFor(e.agentId).RequestModuleInterface<IParcelManagementModule>();
            if (parcelManagement == null)
                return false;
            ILandObject lob = parcelManagement.GetLandObject(e.parcelLocalID);

            if (lob != null)
            {
                UUID AuthorizedID = lob.LandData.AuthBuyerID;
                int saleprice = lob.LandData.SalePrice;
                UUID pOwnerID = lob.LandData.OwnerID;

                bool landforsale = ((lob.LandData.Flags &
                                     (uint)
                                     (ParcelFlags.ForSale | ParcelFlags.ForSaleObjects |
                                      ParcelFlags.SellParcelObjects)) != 0);
                if ((AuthorizedID == UUID.Zero || AuthorizedID == e.agentId) && e.parcelPrice >= saleprice &&
                    landforsale)
                {
                    if (m_connector.UserCurrencyTransfer(lob.LandData.OwnerID, e.agentId,
                                                         (uint) saleprice, "Land Buy", TransactionType.LandSale,
                                                         UUID.Zero))
                    {
                        e.parcelOwnerID = pOwnerID;
                        e.landValidated = true;
                        return true;
                    }
                    else
                    {
                        e.landValidated = false;
                    }
                }
            }
            return false;
        }

        #endregion

        #region systemUsers
        /// <summary>
        /// Checks and creates the banker and marketplace user.
        /// </summary>
        private void CheckUserInfo()
        {
            if (m_accountService == null)
                return;

            CheckBankerUserInfo ();
            CheckMarketplaceUserInfo ();

        }

        private void CheckBankerUserInfo()
        {
            UserAccount banInfo = m_accountService.GetUserAccount (null, UUID.Parse (Constants.BankerUUID));
            var banPassword = Utilities.RandomPassword.Generate (2, 1, 0);

            if (banInfo == null)
            {
                MainConsole.Instance.Warn ("Creating the Banker user '" + BankerName + "'");

                var error = m_accountService.CreateUser (
                    (UUID)Constants.BankerUUID,             // UUID
                    UUID.Zero,                              // ScopeID
                    BankerName,                             // Name
                    Util.Md5Hash (banPassword),             // password
                    "");                                    // email

                if (error == "")
                {
                    SaveBankerPassword (banPassword);
                    MainConsole.Instance.Info (" The password for '" + BankerName + "' is : " + banPassword);

                } else
                {
                    MainConsole.Instance.Warn (" Unable to create the Banker user : " + error);
                    return;
                }

                //set as "Maintenace" level
                var account = m_accountService.GetUserAccount (null, UUID.Parse (Constants.BankerUUID));
                account.UserLevel = 250;
                account.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                bool success = m_accountService.StoreUserAccount (account);

                if (success)
                    MainConsole.Instance.Info (" The Banker user has been elevated to 'Maintenance' level");

                return;

            }

            // we already have the Governor account.. verify details in case of a configuration change
            if (banInfo.Name != BankerName)
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService>();

                banInfo.Name = BankerName;
                bool updatePass = authService.SetPassword(banInfo.PrincipalID, "UserAccount", banPassword);
                bool updateAcct = m_accountService.StoreUserAccount(banInfo);

                if (updatePass && updateAcct)
                {
                    SaveBankerPassword(banPassword);
                    MainConsole.Instance.InfoFormat(" The Banker user has been updated to '{0}'", BankerName);
                }
                else
                    MainConsole.Instance.Warn(" There was a problem updating the Banker user");
            }
        }

        private void CheckMarketplaceUserInfo()
        {
            UserAccount marInfo = m_accountService.GetUserAccount(null, UUID.Parse(Constants.MarketplaceUUID));
            var marPassword = Utilities.RandomPassword.Generate(2, 1, 0);

            if (marInfo == null)
            {
                MainConsole.Instance.Warn("Creating the Marketplace user '" + MarketplaceName + "'");

                var error = m_accountService.CreateUser(
                    (UUID)Constants.MarketplaceUUID,        // UUID
                    UUID.Zero,                              // ScopeID
                    MarketplaceName,                        // Name
                    Util.Md5Hash(marPassword),              // password
                    "");                                    // email

                if (error == "")
                {
                    SaveMarketplacePassword(marPassword);
                    MainConsole.Instance.Info(" The password for '" + MarketplaceName + "' is : " + marPassword);

                }
                else
                {
                    MainConsole.Instance.Warn(" Unable to create the Marketplace user : " + error);
                    return;
                }

                //set as "Maintenace" level
                var account = m_accountService.GetUserAccount(null, UUID.Parse(Constants.MarketplaceUUID));
                account.UserLevel = 250;
                account.UserFlags = Constants.USER_FLAG_CHARTERMEMBER;
                bool success = m_accountService.StoreUserAccount(account);

                if (success)
                    MainConsole.Instance.Info(" The Marketplace user has been elevated to 'Maintenance' level");

                return;

            }

            // we already have the Marketplace account.. verify details in case of a configuration change
            if (marInfo.Name != MarketplaceName)
            {
                IAuthenticationService authService = m_registry.RequestModuleInterface<IAuthenticationService>();

                marInfo.Name = MarketplaceName;
                bool updatePass = authService.SetPassword(marInfo.PrincipalID, "UserAccount", marPassword);
                bool updateAcct = m_accountService.StoreUserAccount(marInfo);

                if (updatePass && updateAcct)
                {
                    SaveMarketplacePassword(marPassword);
                    MainConsole.Instance.InfoFormat(" The Marketplace user has been updated to '{0}'", MarketplaceName);
                }
                else
                    MainConsole.Instance.Warn(" There was a problem updating the Marketplace user");
            }
        }

        private void SaveBankerPassword(string password)
        {
            const string passFile = Constants.DEFAULT_DATA_DIR + "/Banker.txt";

            if (File.Exists(passFile))
                File.Delete(passFile);

            using (var pwFile = new StreamWriter(passFile))
            {
                pwFile.WriteLine("Banker user   : '" + BankerName + "' was created: " + Culture.LocaleLogStamp());
                pwFile.WriteLine("Password        : " + password);
            }
        }

        private void SaveMarketplacePassword(string password)
        {
            const string passFile = Constants.DEFAULT_DATA_DIR + "/Marketplace.txt";

            if (File.Exists(passFile))
                File.Delete(passFile);

            using (var pwFile = new StreamWriter(passFile))
            {
                pwFile.WriteLine("Marketplace user   : '" + MarketplaceName + "' was created: " + Culture.LocaleLogStamp());
                pwFile.WriteLine("Password        : " + password);
            }
        }

        #endregion 
        
        #region IMoneyModule Members

        public int UploadCharge
        {
            get { return Config.PriceUpload; }
        }

        public int GroupCreationCharge
        {
            get { return Config.PriceGroupCreate; }
        }

        public int DirectoryFeeCharge
        {
            get { return Config.PriceDirectoryFee; }
        }

        public int ClientPort 
        {
            get  { return Config.ClientPort; }
        }

        public bool ObjectGiveMoney(UUID objectID, string objectName, UUID fromID, UUID toID, int amount)
        {
            return m_connector.UserCurrencyTransfer(toID, fromID, UUID.Zero, "", objectID, objectName, (uint) amount, "Object payment",
                                                    TransactionType.ObjectPays, UUID.Zero);
        }

        public int Balance(UUID agentID)
        {
            return (int) m_connector.GetUserCurrency(agentID).Amount;
        }

        public bool Charge(UUID agentID, int amount, string text, TransactionType type)
        {
            return m_connector.UserCurrencyTransfer(UUID.Zero, agentID, (uint)amount, text,
                                                    type, UUID.Zero);
        }

        public event ObjectPaid OnObjectPaid;

        public void FireObjectPaid(UUID objectID, UUID agentID, int amount)
        {
            if (OnObjectPaid != null)
                OnObjectPaid(objectID, agentID, amount);
        }

        public bool Transfer(UUID toID, UUID fromID, int amount, string description, TransactionType type)
        {
            return m_connector.UserCurrencyTransfer(toID, fromID, (uint) amount, description, type,
                                                    UUID.Zero);
        }

        public bool Transfer(UUID toID, UUID fromID, UUID toObjectID, string toObjectName, UUID fromObjectID, 
            string fromObjectName, int amount, string description, TransactionType type)
        {
            bool result = m_connector.UserCurrencyTransfer(toID, fromID, toObjectID, toObjectName, 
                fromObjectID, fromObjectName, (uint)amount, description, type, UUID.Zero);
            if (toObjectID != UUID.Zero)
            {
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager>();
                if (manager != null)
                {
                    foreach (IScene scene in manager.Scenes)
                    {
                        ISceneChildEntity ent = scene.GetSceneObjectPart(toObjectID);
                        if (ent != null)
                            FireObjectPaid(toObjectID, fromID, amount);
                    }
                }
            }
            return result;
        }

        public List<GroupAccountHistory> GetTransactions(UUID groupID, UUID agentID, int currentInterval,
                                                         int intervalDays)
        {
            return new List<GroupAccountHistory>();
        }

        public GroupBalance GetGroupBalance(UUID groupID)
        {
            return m_connector.GetGroupBalance(groupID);
        }

        public uint NumberOfTransactions(UUID toAgent, UUID fromAgent)
        {
            return m_connector.NumberOfTransactions(toAgent, fromAgent);
        }

        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetTransactionHistory(toAgentID, fromAgentID, dateStart, dateEnd, start, count);
        }

        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, UUID fromAgentID, int period, string periodType)
        {
            return m_connector.GetTransactionHistory (toAgentID, fromAgentID, period, periodType);
        }
            
        public List<AgentTransfer> GetTransactionHistory(UUID toAgentID, int period, string periodType)
        {
            return m_connector.GetTransactionHistory(toAgentID, period, periodType);
        }

        public List<AgentTransfer> GetTransactionHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetTransactionHistory(dateStart, dateEnd, start, count);
        }

        public List<AgentTransfer> GetTransactionHistory(int period, string periodType, uint? start, uint? count)
        {
            return m_connector.GetTransactionHistory(period, periodType, start, count);
        }
 

        public uint NumberOfPurchases(UUID UserID)
        {
            return m_connector.NumberOfPurchases(UserID);
        }

        public List<AgentPurchase> GetPurchaseHistory(UUID userID, DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetPurchaseHistory(userID, dateStart, dateEnd, start, count);
        }

        public List<AgentPurchase> GetPurchaseHistory(UUID toAgentID, int period, string periodType)
        {
            return m_connector.GetPurchaseHistory(toAgentID, period, periodType);
        }

        public List<AgentPurchase> GetPurchaseHistory(DateTime dateStart, DateTime dateEnd, uint? start, uint? count)
        {
            return m_connector.GetPurchaseHistory(dateStart, dateEnd, start, count);
        }

        public List<AgentPurchase> GetPurchaseHistory (int period, string periodType, uint? start, uint? count)
        {
            return m_connector.GetPurchaseHistory(period, periodType, start, count);
        }

        #endregion

        #region Client Members

        void OnNewClient(IClientAPI client)
        {
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnMoneyTransferRequest += ProcessMoneyTransferRequest;
        }

        void OnMakeRootAgent(IScenePresence presence)
        {
            presence.ControllingClient.SendMoneyBalance(UUID.Zero, true, new byte[0],
                                                        (int) m_connector.GetUserCurrency(presence.UUID).Amount);
        }

        protected void OnClosingClient(IClientAPI client)
        {
            client.OnEconomyDataRequest -= EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest -= SendMoneyBalance;
            client.OnMoneyTransferRequest -= ProcessMoneyTransferRequest;
        }

        void ProcessMoneyTransferRequest(UUID fromID, UUID toID, int amount, int type, string description)
        {
            if (toID != UUID.Zero)
            {
                ISceneManager manager = m_registry.RequestModuleInterface<ISceneManager>();
                if (manager != null)
                {
                    bool paid = false;
                    foreach (IScene scene in manager.Scenes)
                    {
                        ISceneChildEntity ent = scene.GetSceneObjectPart(toID);
                        if (ent != null)
                        {
                            bool success = m_connector.UserCurrencyTransfer(ent.OwnerID, fromID, ent.UUID, ent.Name, UUID.Zero, "",
                                (uint)amount, description, (TransactionType)type, UUID.Random());
                            if (success)
                                FireObjectPaid(toID, fromID, amount);
                            paid = true;
                            break;
                        }
                    }
                    if(!paid)
                    {
                        m_connector.UserCurrencyTransfer(toID, fromID, (uint)amount, description,
                                                    (TransactionType)type, UUID.Random());
                    }
                }
            }
        }

        bool ValidateLandBuy(EventManager.LandBuyArgs e)
        {
            return m_connector.UserCurrencyTransfer(e.parcelOwnerID, e.agentId,
                                                    (uint) e.parcelPrice, "Land Purchase", TransactionType.LandSale,
                                                    UUID.Random());
        }

        void EconomyDataRequestHandler(IClientAPI remoteClient)
        {
            if (Config == null)
            {
                remoteClient.SendEconomyData(0, remoteClient.Scene.RegionInfo.ObjectCapacity,
                                             remoteClient.Scene.RegionInfo.ObjectCapacity,
                                             0, 0,
                                             0, 0,
                                             0, 0,
                                             0,
                                             0, 0,
                                             0, 0,
                                             0,
                                             0, 0);
            }
            else
                remoteClient.SendEconomyData(0, remoteClient.Scene.RegionInfo.ObjectCapacity,
                                             remoteClient.Scene.RegionInfo.ObjectCapacity,
                                             0, Config.PriceGroupCreate,
                                             0, 0,
                                             0, 0,
                                             0,
                                             0, 0,
                                             0, 0,
                                             Config.PriceUpload,
                                             0, 0);
        }

        void SendMoneyBalance(IClientAPI client, UUID agentId, UUID sessionId, UUID transactionId)
        {
            if (client.AgentId == agentId && client.SessionId == sessionId)
            {
                var cliBal = (int)m_connector.GetUserCurrency (client.AgentId).Amount;   
                client.SendMoneyBalance (transactionId, true, new byte[0], cliBal);
            }
            else
                client.SendAlertMessage("Unable to send your money balance to you!");
        }

        #endregion

        #region Service Members

        OSDMap syncRecievedService_OnMessageReceived(OSDMap message)
        {
            string method = message["Method"];
            if (method == "UpdateMoneyBalance")
            {
                UUID agentID = message["AgentID"];
                int Amount = message["Amount"];
                string Message = message["Message"];
                UUID TransactionID = message["TransactionID"];
                IDialogModule dialogModule = GetSceneFor(agentID).RequestModuleInterface<IDialogModule>();
                IScenePresence sp = GetSceneFor(agentID).GetScenePresence(agentID);
                if (sp != null)
                {
                    if (dialogModule != null && !string.IsNullOrEmpty(Message))
                    {
                        dialogModule.SendAlertToUser(agentID, Message);
                    }
                    sp.ControllingClient.SendMoneyBalance(TransactionID, true, Utils.StringToBytes(Message), Amount);
                }
            }
            else if (method == "GetLandData")
            {
                MainConsole.Instance.Info (message);

                UUID agentID = message["AgentID"];
                IScene region = GetSceneFor (agentID);
                MainConsole.Instance.Info ("Region: " + region.RegionInfo.RegionName);

                IParcelManagementModule parcelManagement = region.RequestModuleInterface<IParcelManagementModule>();
                if (parcelManagement != null)
                {
                    IScenePresence sp = region.GetScenePresence(agentID);
                    if (sp != null)
                    {
                        MainConsole.Instance.InfoFormat ("sp parcel UUID: {0} Pos: {1}, {2}",
                            sp.CurrentParcelUUID, sp.AbsolutePosition.X, sp.AbsolutePosition.Y);
                        
                        ILandObject lo = sp.CurrentParcel;
                        if (lo == null)
                        {
                            // try for a position fix
                            lo = parcelManagement.GetLandObject ((int)sp.AbsolutePosition.X, (int)sp.AbsolutePosition.Y);
                        }

                        if (lo != null)
                        {   
                            if ((lo.LandData.Flags & (uint)ParcelFlags.ForSale) == (uint)ParcelFlags.ForSale)
                            {
                                if (lo.LandData.AuthBuyerID != UUID.Zero && lo.LandData.AuthBuyerID != agentID)
                                    return new OSDMap () { new KeyValuePair<string, OSD> ("Success", false) };
                                OSDMap map = lo.LandData.ToOSD ();
                                map ["Success"] = true;
                                return map;
                            }
                        }
                    }
                }
                return new OSDMap() {new KeyValuePair<string, OSD>("Success", false)};
            }
            return null;
        }

        IScene GetSceneFor(UUID userID)
        {
            foreach (IScene scene in m_scenes)
            {
                var sp = scene.GetScenePresence (userID);
                if (sp != null && !sp.IsChildAgent)
                    return scene;
            }
            if (m_scenes.Count == 0)
            {
                MainConsole.Instance.Debug ("User is not present in any region??");
                return null;
            }

            MainConsole.Instance.Debug ("Returning scene[0]: " + m_scenes [0].RegionInfo.RegionName);
            return m_scenes[0];
        }

        /// <summary>
        ///     All message for money actually go through this function. Which also update the balance
        /// </summary>
        /// <param name="toId"></param>
        /// <param name="message"></param>
        /// <param name="transactionId"></param>
        /// <returns></returns>
        public bool SendGridMessage(UUID toId, string message, UUID transactionId)
        {
            IDialogModule dialogModule = GetSceneFor(toId).RequestModuleInterface<IDialogModule>();
            if (dialogModule != null)
            {
                IScenePresence icapiTo = GetSceneFor(toId).GetScenePresence(toId);
                if (icapiTo != null)
                {
                    icapiTo.ControllingClient.SendMoneyBalance(transactionId, true, Utils.StringToBytes(message),
                                                               (int) m_connector.GetUserCurrency(icapiTo.UUID).Amount);
                    dialogModule.SendAlertToUser(toId, message);
                }

                return true;
            }
            return false;
        }

        #endregion
    }
}