using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using Life.VehicleSystem;
using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assurance
{
    public class Assurance : ModKit.ModKit
    {
        public Assurance(IGameAPI aPI) : base(aPI)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Zerox");
        }
        public static Config config;
        public class Config
        {
            public int IdAssurreur;
            public int PriceToApply;
            public int Price;
            public int PriceForBiz;
        }
        public void CreateConfig()
        {
            string directoryPath = pluginsPath + $"/{AssemblyHelper.GetName()}";

            string configFilePath = directoryPath + "/config.json";

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (!File.Exists(configFilePath))
            {
                var defaultConfig = new Config
                {
                    IdAssurreur = 0,
                    Price = 100,
                    PriceForBiz = 80,
                    PriceToApply = 100,
                };
                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configFilePath, jsonContent);
            }

            config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFilePath));
        }
        public override void OnPluginInit()
        {
            base.OnPluginInit();
            CreateConfig();
            Orm.RegisterTable<AssuranceOrm>();
            Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
            new SChatCommand("/assurance", "assurance", "/assurance", (player, args) => { OnSlashAssurance(player); }).Register();
            new SChatCommand("/cassurance", "assurance", "/cassurance", (player, args) => { if (player.HasBiz && player.GetActivity() == Life.BizSystem.Activity.Type.LawEnforcement) ControlAssurance(player, player.setup.driver.vehicle); }).Register();
            Nova.server.OnHourPassedEvent += new Action(OnHour);
        }
        public async void ControlAssurance(Player player, Vehicle vehicle)
        {
            if (vehicle != null)
            {
                var element = await AssuranceOrm.Query(x => x.VehicleDbId == vehicle.VehicleDbId);
                if (element.Any())
                {
                    player.SendText("<color=red>[Assurance]</color> Assurance trouvé !");
                }
                else
                {
                    player.SendText("<color=red>[Assurance]</color> Aucune assurance trouvé !");
                }
            }
            else
            {
                player.SendText("<color=red>[Assurance]</color> Tu n'es pas dans un véhicule !");
            }
        }
        public async void OnHour()
        {
            foreach (var vehicles in Nova.v.vehicles)
            {
                var element = await AssuranceOrm.Query(x => x.VehicleDbId == vehicles.vehicleId);
                if (element.Any())
                {
                    var player = Nova.server.GetPlayer(vehicles.permissions.owner.characterId);
                    if (player.isInGame)
                    {
                        if (vehicles.bizId == 0)
                        {
                            player.SendText($"<color=red>[Assurance]</color> <b><color=#60a832>{config.Price}</color></b> € ont été enlevés à ton compte en banque pour l'assurance de ta voiture immatriculé : <color=#3632a8>{vehicles.plate}</color> !");
                            var biz = await LifeDB.FetchBiz(config.IdAssurreur);
                            var playerOwner = Nova.server.GetPlayer(biz.OwnerId);
                            playerOwner.SendText($"<color=red>[Assurance]</color> <b><color=#60a832>{config.Price}</color></b> € ont été ajoutées à la banque de ton entreprise car <color=#3632a8>{player.FullName}</color> a payé son assurance !");
                            biz.Bank += config.Price;
                            biz.Save();
                            player.AddBankMoney(-config.Price);
                            await player.Save();
                        }
                        else
                        {
                            player.SendText($"<color=red>[Assurance]</color> <b><color=#60a832>{config.PriceForBiz}</color></b> € ont été enlevés du compte en banque de ton entreprise pour l'assurance de ta voiture immatriculé : <color=#3632a8>{vehicles.plate}</color> !");
                            var biz = await LifeDB.FetchBiz(config.IdAssurreur);
                            var bizPlayer = await LifeDB.FetchBiz(vehicles.bizId);
                            bizPlayer.Bank -= config.PriceForBiz;
                            biz.Bank += config.PriceForBiz;
                            biz.Save();
                            bizPlayer.Save();
                            var playerOwner = Nova.server.GetPlayer(biz.OwnerId);
                            playerOwner.SendText($"<color=red>[Assurance]</color> <b><color=#60a832>{config.PriceForBiz}</color></b> € ont été ajoutées à la banque de ton entreprise car l'entreprise <color=#3632a8>{bizPlayer.BizName}</color> a payé son assurance !");
                        }
                    }
                }
            }
        }
        public void OnSlashAssurance(Player player)
        {
            if (player.setup.driver.vehicle.VehicleDbId != 0)
            {
                if (player.biz.Id != config.IdAssurreur)
                {
                    player.SendText("<color=red>[Assurance]</color> Vous n'êtes pas assureur !");
                    return;
                }
                else
                {
                    UIPanel panel = new UIPanel("<color=#1eb04c>Assurance</color>", UIPanel.PanelType.Tab);
                    panel.AddTabLine($"<color=#d1ac17>Mettre l'assurance ({config.PriceToApply}€)</color>", ui => { player.ClosePanel(ui); SetAssurance(player, player.setup.driver.vehicle); });
                    panel.AddTabLine("<color=#d1172a>Supprimer l'assurance</color>", ui => { player.ClosePanel(ui); DeleteAssurance(player, player.setup.driver.vehicle); });
                    panel.AddButton("Fermer", ui => player.ClosePanel(ui));
                    panel.AddButton("Valider", ui => ui.SelectTab());
                    player.ShowPanelUI(panel);
                }
            }
            else
            {
                player.SendText("<color=red>[Assurance]</color> Tu n'es pas dans un véhicule !");
            }
        }
        public async void SetAssurance(Player player, Vehicle vehicle)
        {
            var instance = new AssuranceOrm();
            LifeVehicle lifevehicle = Nova.v.GetVehicle(vehicle.VehicleDbId);
            instance.VehicleDbId = vehicle.VehicleDbId;
            var save = await instance.Save();
            if (save)
            {
                player.SendText("<color=red>[HAssurance]</color> Assurance appliqué avec succés !");
                player.AddMoney(-config.PriceToApply, "Assurance");
            }
            else
            {
                player.SendText("<color=red>[Assurance]</color> Une erreur est survenue merci de réessayer ultérirement si le probléme persiste merci d'en parler à un staff !");
            }
        }
        public async void DeleteAssurance(Player player, Vehicle vehicle)
        {
            var element = await AssuranceOrm.Query(x => x.VehicleDbId == vehicle.VehicleDbId);
            if (element.Any())
            {
                foreach (var elements in element)
                {
                    elements.VehicleDbId = 0;
                    await elements.Save();
                    player.SendText("<color=red>[Assurance]</color> Assurance supprimé avec succés !");
                }
            }
            else
            {
                player.SendText("<color=red>[Assurance]</color> Aucune assurance trouvé avec ce véhicule !");
            }
        }
    }
}
