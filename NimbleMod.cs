using System.Collections;
using Modding;
using UnityEngine;

namespace HKNimbleKnight
{
    public class NimbleMod : Mod, ITogglableMod
    {
        public NimbleMod() : base("HK Nimble Knight") { }
        public override string GetVersion() => "1.1.4";

        // Speed originals
        private float _origRunSpeed, _origJumpSpeed, _origDashSpeed;

        // One-shot flags
        private bool _grantScheduled;
        private bool _granted;

        public override void Initialize()
        {
            // Run once when the player character is created
            On.HeroController.Start += HeroController_Start;
        }

        private void HeroController_Start(On.HeroController.orig_Start orig, HeroController self)
        {
            orig(self);

            // === Simple “nimble” movement buffs ===
            _origRunSpeed = self.RUN_SPEED;
            _origJumpSpeed = self.JUMP_SPEED;
            _origDashSpeed = self.DASH_SPEED;

            self.RUN_SPEED *= 1.70f;  // +70% run
            self.JUMP_SPEED *= 1.70f; // +70% jump
            self.DASH_SPEED *= 1.20f; // +20% dash

            // === Schedule the “give all charms after 10s” once per session ===
            if (!_grantScheduled)
            {
                _grantScheduled = true;
                self.StartCoroutine(GrantAllCharmsDelayed());
                Modding.Logger.Log("[NimbleMod] Scheduled charm grant in 10s.");
            }
        }

        private IEnumerator GrantAllCharmsDelayed()
        {
            // Wait 10 seconds in-game
            yield return new WaitForSeconds(10f);

            // Make sure PlayerData exists (it should, but be defensive)
            var pd = PlayerData.instance;
            int guard = 120;
            while (pd == null && guard-- > 0)
            {
                yield return null;
                pd = PlayerData.instance;
            }
            if (pd == null)
            {
                Modding.Logger.Log("[NimbleMod] PlayerData null; aborting charm grant.");
                yield break;
            }

            if (_granted) yield break;
            _granted = true;

            // === Unlock Charms tab and grant all charms ===
            pd.hasCharm = true; // gate that shows the Charms tab

            for (int id = 1; id <= 40; id++)
            {
                pd.SetBool($"gotCharm_{id}", true);  // own it
                pd.SetBool($"newCharm_{id}", false); // no NEW! spam
            }

            pd.SetInt("charmsOwned", 40);
            pd.SetInt("charmSlots", 11); // plenty of notches

            // Ask game systems to refresh
            try
            {
                pd.CountCharms();
                HeroController.instance?.CharmUpdate();
            }
            catch { /* safe if these differ between versions */ }

            Modding.Logger.Log("[NimbleMod] Granted all charms & 11 notches.");
        }

        // Toggle support in mod menu
        public void Unload()
        {
            On.HeroController.Start -= HeroController_Start;

            var hc = HeroController.instance;
            if (hc != null)
            {
                if (_origRunSpeed > 0) hc.RUN_SPEED = _origRunSpeed;
                if (_origJumpSpeed > 0) hc.JUMP_SPEED = _origJumpSpeed;
                if (_origDashSpeed > 0) hc.DASH_SPEED = _origDashSpeed;
            }

            _grantScheduled = false;
            _granted = false;

            Modding.Logger.Log("[NimbleMod] Unloaded and reverted speeds.");
        }
    }
}
