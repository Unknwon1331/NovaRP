using GTANetworkAPI;
using GVMP;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;

namespace Crimelife
{
    public class Main : Script
    {
        public static int timeToRestart;
        public static List<Npc> ServerNpcs = new List<Npc>();

        public void InitGameMode()
        {

            NAPI.Server.SetAutoRespawnAfterDeath(false);
            NAPI.Server.SetCommandErrorMessage(" ");
            NAPI.Server.SetGlobalServerChat(false);
            NAPI.Server.SetAutoSpawnOnConnect(false);

            Modules.Instance.LoadAll();

            Logger.Print("");
            Logger.Print("");
            Logger.Print("     N E M E S I S    C R I M E L I F E       S T A R T E D    ");
            Logger.Print("");
            Logger.Print("");

            MySqlHandler.ExecuteSync(new MySqlQuery("UPDATE vehicles SET Parked = 1"));
            WebhookSender.SendMessage("-> " + NAPI.Server.GetServerName(), $"Server Load {NAPI.Server.GetServerName()} - {NAPI.Server.GetServerPort()} - {NAPI.Server.GetGamemodeName()} - {NAPI.Server.GetMaxPlayers()} ", Webhooks.Serverstatus, "Server Started");
            WebhookSender.SendMessage("Nemesis-Crimelife", "Der Server wird in Version 1.1 Gestartet", Webhooks.Serverstatus, "Server Started");
            Logger.Print("Parked all vehicles.");
        }

        public bool IsUserAdministrator()
        {
            bool isAdmin;
            try
            {
                WindowsIdentity user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (UnauthorizedAccessException ex)
            {
                isAdmin = false;
            }
            catch (Exception ex)
            {
                isAdmin = false;
            }
            return isAdmin;
        }

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStartHandler()
        {
            InitGameMode();
            timeToRestart = 15;
            SyncThread.Init();
            SyncThread.Instance.Start();
            WebhookSender.SendMessage("Console", "" + timeToRestart + " -> " + DateTime.Now.ToString("HH':'mm':'ss"), Webhooks.Console, "OnResourceStartHandler");
        }

        public static void OnHourHandler()
        {
            try
            {

                foreach (DbPlayer dbPlayer in PlayerHandler.GetPlayers())
                {


                    if (dbPlayer == null || !dbPlayer.IsValid(true) || dbPlayer.player == null)
                        continue;

                    dbPlayer.SetAttribute("XP", (int)dbPlayer.GetAttributeInt("XP") + 1);
                    dbPlayer.XP = dbPlayer.XP + 1;
                    dbPlayer.RefreshData(dbPlayer);

                    if ((int)dbPlayer.GetAttributeInt("XP") >= dbPlayer.Level * 4)
                    {
                        dbPlayer.SetAttribute("Level", (int)dbPlayer.GetAttributeInt("Level") + 1);
                        dbPlayer.Level = dbPlayer.Level + 1;
                        dbPlayer.RefreshData(dbPlayer);
                        dbPlayer.SendNotification("Glueckwunsch, Sie haben nun Level " + dbPlayer.Level + " erreicht!", 5000, "yellow", "Level aufgestiegen!");
                        dbPlayer.SendNotification("Durch Ihr Levelup haben Sie " + dbPlayer.Level + " erhalten!", 5000, "#2f2f30");
                    }

                    House house = HouseModule.houses.FirstOrDefault((House house2) => house2.TenantsIds.Contains(dbPlayer.Id));

                    if (house != null)
                    {
                        int price = 0;

                        if (house.TenantPrices.ContainsKey(dbPlayer.Id))
                            price = house.TenantPrices[dbPlayer.Id];

                        dbPlayer.SendNotification("Dir wurde dein Mietpreis abgezogen! -" + price.ToDots() + "$");
                        dbPlayer.removeMoney(price);
                    }

                    dbPlayer.addMoney(250000);
                    dbPlayer.SendNotification("Du hast deinen Payday erhalten +250.000$", 5000, "darkgreen", "Kontoüberweisung");

                    PlayerHandler.GetAdminPlayers().ForEach((DbPlayer dbPlayer2) =>
                    {
                        Adminrank adminranks = dbPlayer2.Adminrank;

                        if (dbPlayer.Adminrank.Permission <= 91)
                            dbPlayer.SendNotification("Teammitglied Payday +350.000$", 5000, "red", "Kontoüberweisung");
                        dbPlayer.addMoney(350000);
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Print("[EXCEPTION OnHourSpent] " + ex.Message);
                Logger.Print("[EXCEPTION OnHourSpent] " + ex.StackTrace);
            }
        }

        public static void OnMinHandler()
        {
            try
            {
                MySqlConnection con = new MySqlConnection(Configuration.connectionString);
                con.ClearAllPoolsAsync();
                con.Dispose();

                foreach (DbPlayer dbPlayer in PlayerHandler.GetPlayers())
                {
                    if (dbPlayer == null || !dbPlayer.IsValid(true) || dbPlayer.player == null || dbPlayer.player.IsNull)
                        continue;


                    if (dbPlayer.DeathData.IsDead)
                    {
                        if (dbPlayer.player == null) return;
                        DeathData deathData = dbPlayer.DeathData;
                        DateTime dateTime = deathData.DeathTime;
                        dbPlayer.disableAllPlayerActions(true);
                        dbPlayer.SetInvincible(true);
                        dbPlayer.StopAnimation();
                        dbPlayer.PlayAnimation(33, "combat@damage@rb_writhe", "rb_writhe_loop", 8f);

                        if (DateTime.Now.Subtract(dateTime).TotalMinutes >= 2)
                        {
                            dbPlayer.DeathData = new DeathData
                            {
                                IsDead = false,
                                DeathTime = new DateTime(0)
                            };

                            if (dbPlayer.Dimension == FactionModule.GangwarDimension)
                            {
                                if (GangwarModule.RunningGangwar != null)
                                {
                                    if (dbPlayer.Faction == GangwarModule.RunningGangwar.Attacker)
                                    {
                                        dbPlayer.SpawnPlayer(GangwarModule.RunningGangwar.AttackerSpawn);
                                        dbPlayer.disableAllPlayerActions(false);
                                        dbPlayer.StopAnimation();
                                        dbPlayer.StopScreenEffect("DeathFailOut");
                                        dbPlayer.SendNotification("Du wurdest wiederbelebt!", 3000);
                                        dbPlayer.SetAttribute("Death", 0);
                                        dbPlayer.SetInvincible(false);
                                        dbPlayer.SetHealth(99);
                                        dbPlayer.SetArmor(0);
                                    }
                                    else if (dbPlayer.Faction == GangwarModule.RunningGangwar.Faction)
                                    {
                                        dbPlayer.SpawnPlayer(GangwarModule.RunningGangwar.DefenderSpawn);
                                        dbPlayer.disableAllPlayerActions(false);
                                        dbPlayer.StopAnimation();
                                        dbPlayer.StopScreenEffect("DeathFailOut");
                                        dbPlayer.SendNotification("Du wurdest wiederbelebt!", 3000);
                                        dbPlayer.SetAttribute("Death", 0);
                                        dbPlayer.SetInvincible(false);
                                        dbPlayer.SetHealth(99);
                                        dbPlayer.SetArmor(0);
                                    }
                                }
                            }
                            else
                            {
                                if (dbPlayer.Faction.Id == 0)
                                    dbPlayer.SpawnPlayer(new Vector3(298.08, -584.53, 43.26));
                                else
                                    dbPlayer.SpawnPlayer(dbPlayer.Faction.Spawn);

                                dbPlayer.disableAllPlayerActions(false);
                                dbPlayer.StopAnimation();
                                dbPlayer.StopScreenEffect("DeathFailOut");
                                dbPlayer.SendNotification("Du wurdest wiederbelebt!", 3000);
                                dbPlayer.SetAttribute("Death", 0);
                                dbPlayer.SetInvincible(false);
                                dbPlayer.SetHealth(99);
                                dbPlayer.SetArmor(0);
                                dbPlayer.GetInventoryItems().ForEach((ItemModel itemModel) => dbPlayer.UpdateInventoryItems(itemModel.Name, itemModel.Amount, true));
                                dbPlayer.RemoveAllWeapons(true);
                                dbPlayer.SendNotification("Du hast nun einen Spawnschutz von 30 Sekunden! (" + dbPlayer.Name + ")", 3000);
                                dbPlayer.player.SetSharedData("PLAYER_INVINCIBLE", true);
                                NAPI.Task.Run(() =>
                                {
                                    dbPlayer.SendNotification("Du hast nun keinen Spawnschutz mehr!", 3000);
                                    dbPlayer.player.SetSharedData("PLAYER_INVINCIBLE", false);
                                }, 30000);
                            }
                        }
                    }

                    MySqlQuery mySqlQuery = new MySqlQuery("SELECT * FROM accounts WHERE Id = @userId LIMIT 1");
                    mySqlQuery.AddParameter("@userId", dbPlayer.Id);
                    MySqlResult mySqlReaderCon = MySqlHandler.GetQuery(mySqlQuery);
                    MySqlDataReader reader = mySqlReaderCon.Reader;
                    try
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                if (reader.GetInt32("Fraktion") != dbPlayer.Faction.Id)
                                {
                                    Faction oldfraktion = dbPlayer.Faction;
                                    Faction newfraktion = FactionModule.getFactionById(reader.GetInt32("Fraktion"));

                                    dbPlayer.Faction = newfraktion;
                                    dbPlayer.RefreshData(dbPlayer);
                                }

                                if (reader.GetInt32("Fraktionrank") != dbPlayer.Factionrank)
                                {
                                    dbPlayer.Factionrank = reader.GetInt32("Fraktionrank");
                                    dbPlayer.RefreshData(dbPlayer);
                                }
                                if (reader.GetInt32("Business") != dbPlayer.Business.Id)
                                {
                                    Business businessById = BusinessModule.getBusinessById(reader.GetInt32("Business"));
                                    dbPlayer.Business = businessById;
                                    dbPlayer.RefreshData(dbPlayer);
                                }
                                if (reader.GetInt32("Businessrank") != dbPlayer.Businessrank)
                                {
                                    dbPlayer.Businessrank = reader.GetInt32("Businessrank");
                                    dbPlayer.RefreshData(dbPlayer);
                                }
                                if (reader.GetInt32("Adminrank") != dbPlayer.Adminrank.Permission)
                                {
                                    dbPlayer.Adminrank = AdminrankModule.getAdminrank(reader.GetInt32("adminrank"));
                                    dbPlayer.RefreshData(dbPlayer);
                                }

                                if (reader.GetInt32("Money") != dbPlayer.Money)
                                {
                                    dbPlayer.Money = reader.GetInt32("Money");
                                    dbPlayer.RefreshData(dbPlayer);
                                }
                            }
                        }
                    }
                    finally
                    {
                        reader.Dispose();
                        mySqlReaderCon.Connection.Dispose();
                    }

                }
            }
            catch (Exception ex)
            {
                Logger.Print("[EXCEPTION OnMinuteSpent] " + ex.Message);
                Logger.Print("[EXCEPTION OnMinuteSpent] " + ex.StackTrace);
            }
        }

        public static void OnSecHandler()
        {
            try
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
