using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Personal Card Tables", "bmgjet", "1.0.0")]
    [Description("Card Tables for personal use.")]
    public class PersonalCardTables : RustPlugin
    {
        #region Vars
        private const string permUse = "PersonalCardTables.use";
        private Dictionary<ulong, string> CardTables = new Dictionary<ulong, string>
        {
            { 2521003552,"assets/prefabs/deployable/card table/cardtable.static_configc.prefab" },
            { 2523830417,"assets/prefabs/deployable/card table/cardtable.static_configb.prefab" },
            { 2523833900,"assets/prefabs/deployable/card table/cardtable.static_configd.prefab" }
        };
        static List<string> effects = new List<string>
        {
            "assets/bundled/prefabs/fx/item_break.prefab",
            "assets/bundled/prefabs/fx/impacts/stab/rock/stab_rock_01.prefab"
        };
        private static PersonalCardTables plugin;
        #endregion

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
            {"Name", "Card Table"},
            {"Pickup", "You picked up card table!"},
            {"Receive", "You received card table!"},
            {"Permission", "You need permission to do that!"}
            }, this);
        }

        private void message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) { return; }
            var message = string.Format(lang.GetMessage(key, this, player.UserIDString), args);
            player.ChatMessage(message);
        }
        #endregion

        #region Oxide Hooks
        private void OnServerInitialized()
        {
            plugin = this;
            CheckCardTables();
        }

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        private void Unload()
        {
            effects = null;
            plugin = null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go) { CheckDeploy(go); }

        private void OnHammerHit(BasePlayer player, HitInfo info) { CheckHit(player, info?.HitEntity); }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null)
                return null;

            if (entity.name.Contains("cardtable") && entity.OwnerID != 0)
            {
                CardTableAddon CardTable = entity.GetComponent<CardTableAddon>();
                if (CardTable != null)
                {
                    int GiveDamage = 100;
                    try
                    {
                        string Damage = info.damageProperties.name.ToString();
                        switch (Damage.Split('.')[1])
                        {
                            case "Melee": GiveDamage = 5; break;
                            case "Buckshot": GiveDamage = 9; break;
                            case "Arrow": GiveDamage = 15; break;
                            case "Pistol": GiveDamage = 20; break;
                            case "Rifle": GiveDamage = 25; break;
                        }
                    }
                    catch { }
                    var CurrentHealth = CardTable.CardTableProtection.amounts.GetValue(0);
                    int ChangeHealth = int.Parse(CurrentHealth.ToString()) - GiveDamage;
                    CardTable.CardTableProtection.amounts.SetValue((object)ChangeHealth, 0);
                    if (ChangeHealth <= 0)
                    {
                        foreach (var effect in effects) { Effect.server.Run(effect, entity.transform.position); }
                        entity.Kill();
                    }
                }
            }
            return null;
        }
        #endregion

        #region Core
        private void SpawnCardTable(Vector3 position, Quaternion rotation = default(Quaternion), ulong ownerID = 0, string TablePrefab = "cardtable")
        {
            var CardTable = GameManager.server.CreateEntity(TablePrefab, position, rotation);
            if (CardTable == null) { return; }
            CardTable.OwnerID = ownerID;
            CardTable.gameObject.AddComponent<CardTableAddon>();
            CardTable.Spawn();
        }

        private void CheckCardTables()
        {
            foreach (var CardTable in GameObject.FindObjectsOfType<CardTable>())
            {
                var x = CardTable;
                if (x is CardTable && x.OwnerID != 0 && x.GetComponent<CardTableAddon>() == null)
                {
                    Puts("Found Personal Card Table " + CardTable.ToString() + " " + CardTable.OwnerID.ToString() + " Adding Component");
                    CardTable.gameObject.AddComponent<CardTableAddon>();
                }
            }
        }

        private void GiveCardTable(BasePlayer player, bool pickup = false, int tablesize = 0)
        {
            var item = CreateItem(tablesize);
            if (item != null && player != null)
            {
                item.name = "Card Table";
                player.GiveItem(item);
                message(player, pickup ? "Pickup" : "Receive");
            }
        }

        private Item CreateItem(int tablesize)
        {
            switch (tablesize)
                {
                case 2:
                    return ItemManager.CreateByName("table", 1, 2521003552);
                case 3:
                    return ItemManager.CreateByName("table", 1, 2523830417);
                case 4:
                    return ItemManager.CreateByName("table", 1, 2523833900);
            }
            return ItemManager.CreateByName("cardtable", 1);
        }

        private void CheckDeploy(GameObject obj)
        {
            BaseEntity entity = obj.ToBaseEntity();
            if (entity == null) { return; }
            if (IsCardTable(entity.skinID))
            {
                SpawnCardTable(entity.transform.position, entity.transform.rotation, entity.OwnerID, CardTables[entity.skinID]);
                NextTick(() => { entity?.Kill(); });
            }
            else if (entity.ShortPrefabName.Contains("cardtable"))
            {
                entity.gameObject.AddComponent<CardTableAddon>();
            }
        }

        private void CheckHit(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) { return; }
            if (!entity.ShortPrefabName.Contains("cardtable") && entity.OwnerID !=0) { return; }
            entity.GetComponent<CardTableAddon>()?.TryPickup(player, entity);
        }

        [ChatCommand("cardtable.craft")]
        private void Craft(BasePlayer player, string command, string[] args)
        {
            if (CanCraft(player))
            {
                if (args.Length > 0)
                {
                    GiveCardTable(player, false, int.Parse(args[0]));
                    return;
                }
                GiveCardTable(player);
            }
        }

        private bool CanCraft(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                message(player, "Permission");
                return false;
            }
            return true;
        }
        #endregion

        #region Helpers
        private bool IsCardTable(ulong skin){return CardTables.ContainsKey(skin);}
        #endregion

        #region Command
        [ConsoleCommand("CardTable.give")]
        private void Cmd(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin && arg.Args?.Length > 0)
            {
                var player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
                if (player == null)
                {
                    PrintWarning($"Can't find player with that name/ID! {arg.Args[0]}");
                    return;
                }

                if (arg.Args.Length == 2)
                {
                    GiveCardTable(player, false, int.Parse(arg.Args[1]));
                }
                else
                {
                    GiveCardTable(player);
                }
            }
        }
        #endregion

        #region Scripts
        private class CardTableAddon : MonoBehaviour
        {
            private CardTable cardtable;
            public ulong OwnerId;
            public ProtectionProperties CardTableProtection = ScriptableObject.CreateInstance<ProtectionProperties>();

            private void Awake()
            {
                cardtable = GetComponent<CardTable>();
                CardTableProtection.Add(100f);
                InvokeRepeating("CheckGround", 5f, 5f);
            }

            private void CheckGround()
            {
                RaycastHit rhit;
                var cast = Physics.Raycast(cardtable.transform.position + new Vector3(0, 0.1f, 0), Vector3.down,
                    out rhit, 4f, LayerMask.GetMask("Terrain", "Construction"));
                var distance = cast ? rhit.distance : 3f;
                if (distance > 0.2f) { GroundMissing(); }
            }

            private void GroundMissing()
            {
                foreach (var effect in effects) { Effect.server.Run(effect, cardtable.transform.position); }
                this.DoDestroy();
            }

            public void TryPickup(BasePlayer player, BaseEntity entity)
            {
                this.DoDestroy();
                try
                {
                    switch (entity.PrefabName.Split('_')[1].Split('.')[0])
                    {
                        case "configc":
                            plugin.GiveCardTable(player, true, 2);
                            return;
                        case "configb":
                            plugin.GiveCardTable(player, true, 3);
                            return;
                        case "configd":
                            plugin.GiveCardTable(player, true, 4);
                            return;
                    }
                }
                catch { }
                plugin.GiveCardTable(player, true);
            }

            public void DoDestroy()
            {
                var entity = cardtable;
                try { entity.Kill(); } catch { }
            }
        }
        #endregion
    }
}