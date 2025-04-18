using Life;
using Life.BizSystem;
using ModKit.Interfaces;
using System;
using System.Collections.Generic;
using _menu = AAMenu.Menu;
using Life.Network;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Life.UI;
using UnityEngine;
using Mirror;
using Life.DB;
using static ModKit.Utils.IconUtils;
using System.IO;
using ModKit.Helper;
using Mk = ModKit.Helper.TextFormattingHelper;
using System.Linq.Expressions;
using S.I_PolicePack;
using ModKit.Utils;
using ModKit.Helper.VehicleHelper.Classes;
using static UnityEngine.GraphicsBuffer;
using SQLite;


namespace S.I_PolicePack
{
    public class PV : ModKit.ModKit
    {
        public PV(IGameAPI aPI) : base(aPI)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.2.0", "Zerox_Hue");
        }
        public static Config config;
        public class Config
        {
            public int Prix;
            public int LevelMinimumToViewPV;
        }
        public void CreateConfig()
        {
            string directoryPath = pluginsPath + "/S.I - PV";

            string configFilePath = directoryPath + "/config.json";

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (!File.Exists(configFilePath))
            {
                var defaultConfig = new Config
                {
                    Prix = 100,
                    LevelMinimumToViewPV = 3,
                };
                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configFilePath, jsonContent);
            }

            config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFilePath));
        }
        public override void OnPluginInit()
        {
            base.OnPluginInit();
            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialised");
            Orm.RegisterTable<ContraventionORM>();
            CreateConfig();
            AddTabLineViewHistory();
            AddTabLineLawEnforcement();
        }
        public async void PayPV(UIPanel panel, Player target)
        {
            var element = await ContraventionORM.Query(x => x.Plaque == panel.inputText);
            if (element.Any())
            {
                foreach (var elements in element)
                {
                    foreach (var vehicles in Nova.v.vehicles)
                    {
                        if (vehicles.plate == panel.inputText)
                        {
                            int ownerID = vehicles.permissions.owner.characterId;

                            Player player = Nova.server.GetPlayer(ownerID);
                            if (player.isInGame)
                            {
                                await LifeDB.SendSMS(player.character.Id, "17", player.character.PhoneNumber, Nova.UnixTimeNow(), $"Objet : Contravention De : La République Française \n" +
                                $"Vous avez commis une infraction (Mauvais stationnement) et avez donc reçu une contravion de {config.Prix.ToString()} € ! De L'agent de police : {target.FullName}");
                                var contacts = await LifeDB.FetchContacts(player.character.Id);
                                var Listcontacts = contacts.contacts.Where(contact => contact.number == "17").ToList();
                                player.Notify("PV", "Tu as reçu une contravention regarde tes SMS !", NotificationManager.Type.Success);
                                if (!Listcontacts.Any()) { await LifeDB.CreateContact(player.character.Id, "17", "Contravention"); }
                                player.character.Bank -= config.Prix;
                                elements.Payer = true;
                                await player.Save();
                                await elements.Save();
                            }
                        }
                    }
                }
            }
        }
        public void AddTabLineViewHistory()
        {
            _menu.AddAdminTabLine(PluginInformations, config.LevelMinimumToViewPV, "<color=#2dc24d>Voir l'historique des PV</color>", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                ViewHistory(player);
            });
        }
        public void AddTabLineLawEnforcement()
        {
            _menu.AddBizTabLine(PluginInformations, new List<Activity.Type> { Activity.Type.LawEnforcement }, null, $"<color=#2dc24d>Mettre un PV</color>", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                OnClickPV(player);
            });
        }
        public async void ViewHistory(Player player)
        {
            Panel panel = PanelHelper.Create("Historique des PV", UIPanel.PanelType.TabPrice, player, () => ViewHistory(player));
            var allelements = await ContraventionORM.QueryAll();
            if (allelements.Any())
            {
                foreach (var elements in allelements)
                {
                    panel.AddTabLine($"{Mk.Color(elements.Plaque, Mk.Colors.Orange)}", "", IconUtils.Vehicles.RangeRiver.Id, ui =>
                    {
                        string Etat = elements.Payer ? "Payé" : "Non Payé";
                        Panel panel1 = PanelHelper.Create($"{Mk.Color(elements.Plaque, Mk.Colors.Orange)}", UIPanel.PanelType.Text, player, () => ViewHistory(player));
                        panel1.TextLines.Add($"Plaque : {Mk.Italic(Mk.Color(elements.Plaque, Mk.Colors.Purple))}");
                        panel1.TextLines.Add($"Du Policier : {Mk.Italic(Mk.Color(elements.PolicierName, Mk.Colors.Purple))}");
                        panel1.TextLines.Add($"Date : {Mk.Italic(Mk.Color(elements.Temps.ToString(), Mk.Colors.Purple))}");
                        panel1.TextLines.Add($"Payer : {Mk.Italic(Mk.Color(Etat, Mk.Colors.Purple))}");
                        panel1.CloseButton();
                        panel1.Display();
                    });
                }
            }
            else
            {
                panel.AddTabLine($"{Mk.Color("Aucune Contravention", Mk.Colors.Error)}", "", ItemUtils.GetIconIdByItemId(1112), ui =>
                {
                    panel.Refresh();
                });
            }
            panel.CloseButton();
            panel.AddButton("Valider", ui => panel.SelectTab());
            panel.Display();
        }
        public void OnClickPV(Player player)
        {
            Panel panel = PanelHelper.Create("PV", UIPanel.PanelType.Input, player, () => OnClickPV(player));

            panel.SetInputPlaceholder("Saisissez la plaque du véhicule...");

            panel.AddButton("Fermer", ui => player.ClosePanel(ui));

            panel.AddButton("Valider", ui =>
            {
                player.ClosePanel(ui);
                OnClickValid(player, panel);
            });

            panel.Display();
        }
        public async void OnClickValid(Player player, UIPanel panel)
        {
            var elements = await ContraventionORM.Query(x => x.Plaque == panel.inputText);
            ContraventionORM instance = new ContraventionORM();
            instance.Plaque = panel.inputText;
            instance.Temps = DateTime.Now;
            instance.PolicierName = player.FullName;
            await instance.Save();
            bool result = await instance.Save();
            if (result)
            {
                Debug.Log("Sauvegarde Réussi");
                player.SendText($"<color=#e82727>[PV]</color> Le Pv a bien été appliqué !");
                PayPV(panel, player);
            }
            else
            {
                Debug.Log("Sauvegarde Impossible");
                player.SendText($"<color=#e82727>[PV]</color> Il y'a eu une erreur lors du chargement du Pv merci de réessayer ultérirement si le probléme persiste merci d'en parler à un staff !");
            }
        }
        public override async void OnPlayerSpawnCharacter(Player player, NetworkConnection conn, Characters character)
        {
            base.OnPlayerSpawnCharacter(player, conn, character);
            foreach (var vehicles in Nova.v.vehicles)
            {
                if (vehicles.permissions.owner.characterId == player.character.Id)
                {
                    var queriedelement = await ContraventionORM.Query(x => x.Plaque == vehicles.plate && !x.Payer);
                    bool isInTheORM = queriedelement.Any() ? true : false;
                    if (isInTheORM)
                    {
                        foreach (var elements in queriedelement)
                        {
                            player.Notify("PV", "Tu as reçu une contravention regarde tes SMS !", NotificationManager.Type.Success);
                            await LifeDB.SendSMS(player.character.Id, "17", player.character.PhoneNumber, Nova.UnixTimeNow(), $"Objet : Contravention De : La République Française \n" +
                            $"Vous avez commis une infraction (Mauvais stationnement) et avez donc reçu une contravion de {config.Prix.ToString()} € ! Du policier {elements.PolicierName}");
                            var contacts = await LifeDB.FetchContacts(player.character.Id);
                            var Listcontacts = contacts.contacts.Where(contact => contact.number == "17").ToList();
                            if (!Listcontacts.Any()) { await LifeDB.CreateContact(player.character.Id, "17", "Contravention"); }
                            player.character.Bank -= config.Prix;
                            elements.Payer = true;
                            await player.Save();
                            await elements.Save();
                        }
                    }
                }
            }
        }
    }
}
