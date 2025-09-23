using System.Collections;
using Modding;
using UnityEngine;

namespace HKNimbleKnight
{
    public class NimbleMod : Mod, ITogglableMod
    {
        public NimbleMod() : base("HK Nimble Knight") { }
        public override string GetVersion() => "1.1.13";

        private float _origRunSpeed, _origJumpSpeed, _origDashSpeed;
        private bool _grantScheduled, _granted;

        public override void Initialize()
        {
            On.HeroController.Start += HeroController_Start;
        }

        private void HeroController_Start(On.HeroController.orig_Start orig, HeroController self)
        {
            orig(self);

            // Nimble buffs
            _origRunSpeed  = self.RUN_SPEED;
            _origJumpSpeed = self.JUMP_SPEED;
            _origDashSpeed = self.DASH_SPEED;

            self.RUN_SPEED  *= 1.70f;
            self.JUMP_SPEED *= 1.70f;
            self.DASH_SPEED *= 1.20f;

            if (!_grantScheduled)
            {
                _grantScheduled = true;
                self.StartCoroutine(GrantAllCharmsDelayed());
            }
        }

        private IEnumerator GrantAllCharmsDelayed()
        {
            // Give the save a moment to settle
            yield return new WaitForSeconds(10f);

            var pd = PlayerData.instance;
            int guard = 180;
            while (pd == null && guard-- > 0)
            {
                yield return null;
                pd = PlayerData.instance;
            }
            if (pd == null || _granted) yield break;
            _granted = true;

            // Make sure Charms tab exists
            pd.hasCharm = true;

            // Grant every vanilla charm 1..40; mark not-new; set cost = 1
            for (int id = 1; id <= 40; id++)
            {
                pd.SetBool($"gotCharm_{id}", true);
                pd.SetBool($"newCharm_{id}", false);
                pd.SetInt ($"charmCost_{id}", 1);
            }

            // Force Carefree Melody instead of Grimmchild without hiding other charms
            // (troupe gone => UI shows Melody in slot 40)
            pd.SetBool("troupeInTown", false);
            pd.SetInt ("grimmChildLevel", 4);
            pd.SetBool("gotCharm_40", true);

            // Lots of notches so you can equip everything
            pd.SetInt("charmSlots", 100);

            // --- Force FULL KING’S SOUL (not Void Heart) ---
            pd.SetInt("royalCharmState", 3);   // full King's Soul
            pd.SetBool("gotCharm_36", true);   // own the slot

            // Be explicit about fragment flags
            pd.SetBool("gotWhiteFragmentLeft",  true);
            pd.SetBool("gotWhiteFragmentRight", true);
            pd.SetBool("gotQueenFragment",      true);
            pd.SetBool("gotKingFragment",       true);

            // Clear any Void Heart flags so KS doesn't get replaced
            pd.SetBool("gotVoidHeart", false);
            pd.SetBool("voidHeart",    false);
            pd.SetBool("hasVoidHeart", false);

            // Let the game recalc counts/effects on its own
            try
            {
                pd.CountCharms();
                HeroController.instance?.CharmUpdate();
            }
            catch { }
        }

        public void Unload()
        {
            On.HeroController.Start -= HeroController_Start;

            var hc = HeroController.instance;
            if (hc != null)
            {
                if (_origRunSpeed  > 0) hc.RUN_SPEED  = _origRunSpeed;
                if (_origJumpSpeed > 0) hc.JUMP_SPEED = _origJumpSpeed;
                if (_origDashSpeed > 0) hc.DASH_SPEED = _origDashSpeed;
            }

            _grantScheduled = false;
            _granted = false;
        }
    }
}
