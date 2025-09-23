using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Modding;
using UnityEngine;

namespace HKNimbleKnight
{
    public class NimbleMod : Mod, ITogglableMod
    {
        public NimbleMod() : base("HK Nimble Knight") { }
        public override string GetVersion() => "1.3.5";

        // speed restore
        private float _origRunSpeed, _origJumpSpeed, _origDashSpeed;
        private bool _origCaptured;

        // movement toggle (P key)
        private bool _speedEnabled = true; // buffs ON by default

        // state for progression (continues regardless of P)
        private bool _scheduled;
        private readonly HashSet<int> _given = new HashSet<int>();
        private readonly System.Random _rng = new System.Random();

        // cached beep clip
        private static AudioClip _beepClip;

        // reflection helpers (PlayerData only)
        private static FieldInfo F(PlayerData pd, string n) =>
            pd.GetType().GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static int GetInt(PlayerData pd, string name, int fallback = 0)
        {
            var f = F(pd, name);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(pd);
            return fallback;
        }
        private static void SetInt(PlayerData pd, string name, int value)
        {
            var f = F(pd, name);
            if (f != null && f.FieldType == typeof(int)) f.SetValue(pd, value);
        }
        private static void IncInt(PlayerData pd, string name, int delta)
        {
            var f = F(pd, name);
            if (f != null && f.FieldType == typeof(int))
            {
                int cur = (int)f.GetValue(pd);
                f.SetValue(pd, cur + delta);
            }
        }

        public override void Initialize()
        {
            On.HeroController.Start  += HeroController_Start;
            On.HeroController.Update += HeroController_Update; // listen for P key
        }

        private void HeroController_Start(On.HeroController.orig_Start orig, HeroController self)
        {
            orig(self);

            // capture originals once
            if (!_origCaptured)
            {
                _origRunSpeed  = self.RUN_SPEED;
                _origJumpSpeed = self.JUMP_SPEED;
                _origDashSpeed = self.DASH_SPEED;
                _origCaptured  = true;
            }

            // apply buffs if enabled
            ApplyOrRevertSpeed(self, _speedEnabled);

            // start progression ticker once (independent of P)
            if (!_scheduled)
            {
                _scheduled = true;
                self.StartCoroutine(Ticker());
            }
        }

        private void HeroController_Update(On.HeroController.orig_Update orig, HeroController self)
        {
            orig(self);

            // Toggle movement buffs only (P key)
            if (Input.GetKeyDown(KeyCode.P))
            {
                _speedEnabled = !_speedEnabled;
                ApplyOrRevertSpeed(self, _speedEnabled);
                // optional audible click so you know you toggled; keep your beep:
                Beep();
            }
        }

        private void ApplyOrRevertSpeed(HeroController hc, bool enable)
        {
            if (!_origCaptured || hc == null) return;

            if (enable)
            {
                hc.RUN_SPEED  = _origRunSpeed  * 1.70f;
                hc.JUMP_SPEED = _origJumpSpeed * 1.70f;
                hc.DASH_SPEED = _origDashSpeed * 1.20f;
            }
            else
            {
                hc.RUN_SPEED  = _origRunSpeed;
                hc.JUMP_SPEED = _origJumpSpeed;
                hc.DASH_SPEED = _origDashSpeed;
            }
        }

        private IEnumerator Ticker()
        {
            // let things settle
            yield return new WaitForSeconds(3f);

            var pd = PlayerData.instance;
            int guard = 240;
            while (pd == null && guard-- > 0) { yield return null; pd = PlayerData.instance; }
            if (pd == null) yield break;

            // === Carefree Melody prerequisites (so slot 40 shows Melody, not Grimmchild) ===
            pd.SetBool("troupeInTown", false);
            pd.SetInt ("grimmChildLevel", 4);

            // make sure charms tab exists
            pd.hasCharm = true;

            // upfront: force ALL charm costs to 1 so anything you get later is 1 notch
            for (int id = 1; id <= 40; id++)
            {
                pd.SetInt($"charmCost_{id}", 1);
            }
            // upfront: give you tons of notches
            SetInt(pd, "charmSlots", Math.Max(GetInt(pd, "charmSlots", 0), 100));

            // starter charm so you can open the page immediately
            GiveCharm(pd, 1);
            Beep();

            // random order for remaining 2..40
            var order = Enumerable.Range(2, 39).OrderBy(_ => _rng.Next()).ToList();
            int orderIndex = 0;

            bool giveCharmTurn = false; // next tick = shard/vessel

            while (true)
            {
                // === adjust this for testing (e.g., 2f or 5f). Use 60f for real play. ===
                yield return new WaitForSeconds(60f); // <--- DROP INTERVAL

                // keep notches topped up
                SetInt(pd, "charmSlots", Math.Max(GetInt(pd, "charmSlots", 0), 100));

                if (giveCharmTurn && orderIndex < order.Count)
                {
                    int id = order[orderIndex++];
                    GiveCharm(pd, id);
                    Beep();
                }
                else
                {
                    if (_rng.NextDouble() < 0.5) GiveMaskShard(pd);
                    else                          GiveVesselFragment(pd);
                    Beep();
                }

                giveCharmTurn = !giveCharmTurn;

                // refresh effects/UI
                try
                {
                    pd.CountCharms();
                    HeroController.instance?.CharmUpdate();
                }
                catch { }
            }
        }

        private void GiveCharm(PlayerData pd, int id)
        {
            if (_given.Contains(id)) return;

            // === Special cases FIRST ===
            if (id == 36)
            {
                // King's Soul handling as you verified
                pd.SetInt("royalCharmState", 3);
                pd.SetBool("gotCharm_36", true);

                pd.SetBool("gotWhiteFragmentLeft",  true);
                pd.SetBool("gotWhiteFragmentRight", true);
                pd.SetBool("gotQueenFragment",      true);
                pd.SetBool("gotKingFragment",       true);

                pd.SetBool("gotVoidHeart", false);
                pd.SetBool("voidHeart",    false);
                pd.SetBool("hasVoidHeart", false);
            }
            else if (id == 40)
            {
                // Carefree Melody visibility
                pd.SetBool("troupeInTown", false);
                pd.SetInt ("grimmChildLevel", 4);
                pd.SetBool("gotCharm_40", true);
            }

            // === Standard grant flow ===
            pd.SetBool($"gotCharm_{id}", true);
            pd.SetBool($"newCharm_{id}", false);
            pd.SetInt ($"charmCost_{id}", 1);

            // keep count tidy
            int owned = GetInt(pd, "charmsOwned", 0);
            SetInt(pd, "charmsOwned", Math.Max(owned, _given.Count + 1));

            // keep notches generous
            SetInt(pd, "charmSlots", Math.Max(GetInt(pd, "charmSlots", 0), 100));

            _given.Add(id);
        }

        // 4 mask shards -> +1 mask (HUD refresh via safe public call)
        private void GiveMaskShard(PlayerData pd)
        {
            IncInt(pd, "heartPieces", 1);
            int shards = GetInt(pd, "heartPieces", 0);

            if (shards >= 4)
            {
                SetInt(pd, "heartPieces", shards - 4);
                IncInt(pd, "maxHealthBase", 1);

                var hc = HeroController.instance;
                if (hc != null)
                {
                    // Over-heal to ensure the extra mask appears right away.
                    hc.AddHealth(10);
                }
            }
        }

        // 3 vessel fragments -> +1 vessel (UI refresh via safe public call)
        private void GiveVesselFragment(PlayerData pd)
        {
            IncInt(pd, "vesselFragments", 1);
            int vf = GetInt(pd, "vesselFragments", 0);

            if (vf >= 3)
            {
                SetInt(pd, "vesselFragments", vf - 3);
                IncInt(pd, "soulVessels", 1);
                IncInt(pd, "MPReserveMax", 33);
                IncInt(pd, "maxMPReserve", 33);

                var hc = HeroController.instance;
                if (hc != null)
                {
                    // Nudge Soul UI so the new canister is visible immediately.
                    hc.AddMPCharge(0);
                }
            }
        }

        private void Beep()
        {
            try
            {
                var hc = HeroController.instance;
                if (hc == null) return;

                var src = hc.GetComponent<AudioSource>();
                if (src == null) src = hc.gameObject.AddComponent<AudioSource>();

                if (_beepClip == null)
                    _beepClip = MakeBeepClip(frequency: 880f, durationSeconds: 0.15f); // nice bright beep

                // Loud but not ear-splitting; tweak the 1.25f if you want more/less
                src.PlayOneShot(_beepClip, 1.25f);
            }
            catch { }
        }

        private static AudioClip MakeBeepClip(float frequency, float durationSeconds, int sampleRate = 44100)
        {
            int samples = Mathf.RoundToInt(sampleRate * durationSeconds);
            var clip = AudioClip.Create("NimbleBeep", samples, 1, sampleRate, false);
            var data = new float[samples];
            float twoPiF = 2f * Mathf.PI * frequency;

            // simple sine with quick fade in/out to avoid clicks
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float s = Mathf.Sin(twoPiF * t);

                // 5 ms fade in/out
                float fade = 1f;
                float fadeWindow = 0.005f; // 5ms
                float at = i / (float)sampleRate;
                float rt = (samples - 1 - i) / (float)sampleRate;
                if (at < fadeWindow) fade = at / fadeWindow;
                if (rt < fadeWindow) fade = Mathf.Min(fade, rt / fadeWindow);

                data[i] = s * fade;
            }

            clip.SetData(data, 0);
            return clip;
        }

        public void Unload()
        {
            On.HeroController.Start  -= HeroController_Start;
            On.HeroController.Update -= HeroController_Update;

            var hc = HeroController.instance;
            if (hc != null && _origCaptured)
            {
                // Always restore speeds on unload
                hc.RUN_SPEED  = _origRunSpeed;
                hc.JUMP_SPEED = _origJumpSpeed;
                hc.DASH_SPEED = _origDashSpeed;
            }

            _given.Clear();
            _scheduled = false;
        }
    }
}
