using System;
using System.Collections.Generic;

// PURE C# (no UnityEngine). The voices of the reserve as data: one recipe per species (twenty-two of the twenty-seven; the other five are
// silent on purpose), three for small life (frogs). Each is a few lines: the calls as notes (pitch sweep, length, gap, texture) and then
// when and how far it is heard. Tune a species by editing its entry; nothing else needs to change. Rendering is VoiceSynth, scheduling is
// VoiceRules, playing is VoiceDirector.
//
// Pitch and rhythm are what matter: each call follows how birders describe it (a cardinal's down-slurred "cheer", a chickadee's two
// whistles a tone apart, a barred owl's eight hoots). Timbre is approximate. Note format: N(f0, f1, dur, gap, ...) in Hz and seconds.
namespace WildLens.Sound
{
    public static class VoiceBank
    {
        private static Dictionary<string, VoiceRecipe> _species;
        private static Dictionary<string, VoiceRecipe> _subjects;
        private static Dictionary<string, string> _notes;

        /// <summary>The default voice of a species, or null when it is silent (or unknown).</summary>
        public static VoiceRecipe For(string speciesId)
        {
            Build();
            VoiceRecipe r;
            return speciesId != null && _species.TryGetValue(speciesId, out r) ? r : null;
        }

        /// <summary>The voice of a small-life subject (the frogs), or null.</summary>
        public static VoiceRecipe ForSubject(string subjectId)
        {
            Build();
            VoiceRecipe r;
            return subjectId != null && _subjects.TryGetValue(subjectId, out r) ? r : null;
        }

        /// <summary>The one field-guide sentence about a species' voice for its field notes, or null.</summary>
        public static string Note(string speciesId)
        {
            Build();
            string s;
            return speciesId != null && _notes.TryGetValue(speciesId, out s) ? s : null;
        }

        public static IEnumerable<string> SpeciesIds { get { Build(); return _species.Keys; } }
        public static IEnumerable<string> SubjectIds { get { Build(); return _subjects.Keys; } }

        /// <summary>A fresh, independent copy of a species' default voice for writing onto an asset (null when silent).</summary>
        public static VoiceRecipe Copy(string speciesId)
        {
            var r = For(speciesId);
            return r == null ? null : Clone(r);
        }

        // ------------------------------------------------------------------ the recipes

        private static void Build()
        {
            if (_species != null) return;
            _species = new Dictionary<string, VoiceRecipe>();
            _subjects = new Dictionary<string, VoiceRecipe>();
            _notes = new Dictionary<string, string>();
            Birds();
            Others();
            SmallLife();
        }

        private static void Birds()
        {
            // Songbirds: calm calls from a perch, loudest at dawn, bursting out in the first sun after rain, nearly silent in rain.
            Add("northern_cardinal", "Its song is a bright, whistled cheer, cheer, cheer, then a quick birdie-birdie-birdie.", V(
                song: C("song", 22050, S(Rep(3, N(4000, 2400, .28, .19, dec: .3, buzz: .05)), Rep(5, N(3400, 2300, .10, .05, dec: .3)))),
                call: C("chip", 22050, S(N(5200, 4300, .06, .20, noise: .15, dec: .8), N(5200, 4300, .06, .1, noise: .15, dec: .8))),
                alarm: C("chips", 22050, Rep(5, N(5200, 4200, .055, .09, noise: .2, dec: .8))),
                songShare: .75f, perMin: 2.0f, period: P(0, 4, 1.5f, 0.3f, .8f, 1.2f), rain: .05f, after: 2.2f,
                alarmChance: .4f, answer: .5f, minGap: 6, range: 85, near: 5, vol: .55f));

            Add("blue_jay", "Its call is a harsh, ringing jay! jay!", V(
                call: C("jay", 22050, Rep(2, N(3700, 2300, .30, .24, noise: .45, noiseBw: .3f, buzz: .5f, am: 65, amDepth: .55f, dec: .4f, atk: .02f))),
                alarm: C("scream", 22050, Rep(4, N(3800, 2200, .32, .17, noise: .5f, noiseBw: .3f, buzz: .6f, am: 70, amDepth: .6f, dec: .4f, atk: .02f))),
                songShare: 0, perMin: 1.2f, period: P(0, 2, 1.2f, 0.48f, .9f, .6f), rain: .05f, after: 1.6f,
                alarmChance: .6f, alarmCd: 45, answer: .35f, minGap: 8, range: 90, near: 7, vol: .6f));

            Add("black_capped_chickadee", "Its song is a clear, whistled fee-bee, and its call says its own name: chick-a-dee-dee-dee.", V(
                song: C("fee-bee", 22050, S(N(3750, 3700, .38, .12, dec: .12f, atk: .02f), N(3350, 3300, .46, 0, dec: .15f, atk: .02f))),
                call: C("chick-a-dee", 22050, S(N(4600, 4000, .05, .04, noise: .4f, dec: .8f), N(4100, 3800, .05, .06, noise: .3f, dec: .8f),
                    Rep(3, N(3300, 3000, .15, .06, buzz: .7f, am: 55, amDepth: .5f, dec: .3f)))),
                alarm: C("dees", 22050, S(N(4600, 4000, .05, .04, noise: .4f, dec: .8f), N(4100, 3800, .05, .05, noise: .3f, dec: .8f),
                    Rep(7, N(3300, 3000, .13, .045, buzz: .7f, am: 60, amDepth: .5f, dec: .3f)))),
                songShare: .45f, perMin: 1.5f, period: P(0, 3, 1.5f, 0.36f, 1, .8f), rain: .05f, after: 1.8f,
                alarmChance: .5f, alarmCd: 35, answer: .4f, minGap: 6, range: 60, near: 3, vol: .5f));

            Add("tufted_titmouse", "It whistles a quick peter-peter-peter.", V(
                song: C("peter", 22050, Rep(4, S(N(3350, 3000, .14, .02, dec: .3f), N(2650, 2550, .17, .19, dec: .3f)))),
                call: C("peter-2", 22050, Rep(2, S(N(3350, 3000, .14, .02, dec: .3f), N(2650, 2550, .17, .19, dec: .3f)))),
                songShare: .6f, perMin: 1.6f, period: P(0, 3, 1.5f, 0.36f, 1, .7f), rain: .05f, after: 1.8f,
                alarmChance: 0, answer: .5f, minGap: 6, range: 75, near: 4, vol: .52f));

            Add("american_robin", "Its song is a rich, carolled cheerily, cheer-up, cheerio, rising and falling.", V(
                song: C("carol", 22050, S(
                    N(2400, 2800, .13, .02, buzz: .08f), N(3100, 2500, .10, .02, buzz: .08f), N(2600, 3300, .16, .22, buzz: .08f),
                    N(2300, 3100, .20, .04, buzz: .08f), N(3000, 2400, .14, .26, buzz: .08f),
                    N(2700, 3300, .12, .02, buzz: .08f), N(3300, 2500, .12, .02, buzz: .08f), N(2400, 3000, .18, .3, buzz: .08f))),
                call: C("tut", 22050, S(Rep(3, N(2800, 2300, .05, .11, noise: .25f, dec: .8f)), N(3300, 2900, .22, 0, dec: .2f))),
                alarm: C("tut-tut", 22050, Rep(6, N(2800, 2300, .05, .08, noise: .25f, dec: .8f))),
                songShare: .8f, perMin: 2.2f, period: P(0, 5, 1.4f, 0.24f, .9f, 2.0f), rain: .08f, after: 3.0f,
                alarmChance: .3f, alarmCd: 40, answer: .55f, minGap: 5, range: 90, near: 5, vol: .55f));

            Add("american_crow", "Its call is a hoarse caw, caw, caw.", V(
                song: C("caw-caw-caw", 16000, Rep(3, N(1250, 950, .34, .30, noise: .3f, noiseBw: .35f, buzz: .9f, am: 110, amDepth: .65f, dec: .45f, atk: .02f))),
                call: C("caw", 16000, N(1250, 950, .36, 0, noise: .3f, noiseBw: .35f, buzz: .9f, am: 110, amDepth: .65f, dec: .45f, atk: .02f)),
                songShare: .55f, perMin: 1.1f, period: P(0, 1.5f, 1.0f, 0.48f, 1.5f, 2.4f), rain: .2f, after: 1.3f, rest: true, flight: true,
                alarmChance: 0, answer: .45f, minGap: 8, range: 110, near: 9, vol: .62f));

            Add("american_goldfinch", "In flight it calls po-ta-to-chip, and it twitters like a wild canary.", V(
                song: C("twitter", 22050, Twitter()),
                call: C("po-ta-to-chip", 22050, S(N(4300, 5200, .07, .05, dec: .3f), N(4600, 5000, .06, .05, dec: .3f), N(4300, 4900, .06, .05, dec: .3f), N(5000, 5900, .09, 0, dec: .3f))),
                songShare: .35f, perMin: 1.4f, period: P(0, 2, 2, 0.6f, 1, .7f), rain: .05f, after: 1.8f, rest: true, flight: true,
                answer: .4f, minGap: 6, range: 75, near: 3.5f, vol: .5f));

            Add("song_sparrow", "Its song opens with three clear notes and ends in a buzzy trill.", V(
                song: C("song", 22050, S(Rep(3, N(3800, 3800, .14, .07, dec: .2f)),
                    N(3700, 3500, .38, .05, buzz: .3f, am: 32, amDepth: 1f, dec: .1f),
                    N(3000, 4500, .05, .03), N(4500, 3000, .06, .03), N(3300, 4200, .05, .03), N(4300, 3200, .06, .03), N(3500, 4000, .05, 0))),
                call: C("chimp", 22050, N(4200, 3300, .07, 0, noise: .2f, dec: .8f)),
                songShare: .85f, perMin: 1.8f, period: P(0, 4, 1.5f, 0.36f, 1, .7f), rain: .05f, after: 2.0f,
                alarmChance: 0, answer: .5f, minGap: 6, range: 80, near: 4, vol: .52f));

            Add("red_winged_blackbird", "From the reeds it gives a gurgling conk-la-REE, ending in a rasping trill.", V(
                song: C("conk-la-ree", 22050, S(N(1800, 2200, .07, .04, buzz: .6f, noise: .1f), N(2500, 3200, .07, .03, buzz: .5f),
                    N(3900, 3300, .55, 0, noise: .35f, noiseBw: .25f, am: 55, amDepth: .8f, buzz: .6f, atk: .02f, dec: .15f))),
                call: C("check", 22050, N(3000, 2500, .06, 0, noise: .4f, buzz: .5f, dec: .8f)),
                songShare: .8f, perMin: 2.0f, period: P(0, 4, 1.6f, 0.48f, 1, 1), rain: .05f, after: 1.8f,
                alarmChance: 0, answer: .5f, minGap: 6, range: 95, near: 5, vol: .58f));

            Add("eastern_phoebe", "It says its name in a raspy fee-bee.", V(
                song: C("fee-bee", 22050, S(N(3500, 3900, .18, .06, buzz: .3f, dec: .2f), N(3300, 3000, .26, 0, noise: .2f, am: 80, amDepth: .6f, buzz: .5f, dec: .3f))),
                songShare: 1f, perMin: 1.6f, period: P(0, 3, 1.5f, 0.42f, .9f, .8f), rain: .05f, after: 1.8f,
                alarmChance: 0, answer: .4f, minGap: 6, range: 70, near: 4, vol: .5f));

            Add("belted_kingfisher", "Its call is a dry, rattling chatter.", V(
                call: C("rattle", 22050, N(3000, 2500, 1.15f, 0, noise: .7f, noiseBw: .5f, buzz: .4f, am: 17, amDepth: 1f, atk: .03f, dec: .1f)),
                songShare: 0, perMin: .8f, period: P(0, 1.2f, 1.3f, 0.6f, 1, .8f), rain: .05f, after: 1.4f, rest: true, flight: true,
                answer: .3f, minGap: 10, range: 100, near: 8, vol: .55f));

            Add("louisiana_waterthrush", "Its song is a ringing run of slurred, falling whistles that carries over the water.", V(
                song: C("song", 22050, S(N(5000, 4100, .22, .12, dec: .3f), N(4800, 3900, .22, .12, dec: .3f), N(4600, 3700, .22, .12, dec: .3f), N(4400, 3500, .24, .16, dec: .3f),
                    N(3300, 4800, .05, .02), N(4800, 3300, .05, .02), N(3400, 4600, .05, .02), N(4600, 3400, .05, .02), N(3600, 4400, .05, .02), N(4400, 3300, .06, 0))),
                songShare: 1f, perMin: 1.4f, period: P(0, 3, 1.5f, 0.48f, .8f, .6f), rain: .05f, after: 2.5f,
                answer: .5f, minGap: 7, range: 100, near: 6, vol: .6f));

            Add("eastern_bluebird", "Its voice is a soft, low, warbled chur-lee, chur-wi.", V(
                song: C("warble", 22050, S(N(2000, 2250, .13, .03, buzz: .35f, vib: 25, vibDepth: .01f), N(2550, 2000, .18, .24, buzz: .3f),
                    N(2000, 2200, .12, .03, buzz: .35f), N(2400, 1800, .20, .24, buzz: .3f), N(2000, 2200, .12, .03, buzz: .35f), N(2650, 2100, .2, 0, buzz: .3f))),
                call: C("chur-lee", 22050, S(N(2000, 2250, .13, .03, buzz: .35f), N(2550, 2000, .18, 0, buzz: .3f))),
                songShare: .5f, perMin: 1.0f, period: P(0, 3, 1.5f, 0.42f, 1, .6f), rain: .05f, after: 2.5f,
                answer: .35f, minGap: 8, range: 55, near: 3, vol: .42f));

            Add("spotted_sandpiper", "Its call is a thin, high peet-weet.", V(
                call: C("peet-weet", 22050, Rep(2, S(N(3800, 4300, .10, .09, dec: .3f), N(3600, 4400, .16, .38, dec: .3f)))),
                song: C("peet-weet-weet", 22050, S(N(3800, 4300, .10, .09, dec: .3f), N(3600, 4400, .16, .09, dec: .3f), N(3600, 4400, .16, 0, dec: .3f))),
                songShare: .4f, perMin: 1.2f, period: P(0, 1.5f, 1.5f, 0.6f, 1, .8f), rain: .05f, after: 1.6f, rest: true, flight: true,
                answer: .3f, minGap: 8, range: 60, near: 3, vol: .45f));
        }

        private static void Others()
        {
            // Raptor, owl and big birds: carrying, rarer.
            Add("red_tailed_hawk", "Its cry is a hoarse, falling scream, keeee-arrr, from high overhead.", V(
                call: C("scream", 22050, S(N(3000, 3250, .22, -.03f, noise: .3f, noiseBw: .3f, buzz: .6f, am: 40, amDepth: .4f, atk: .02f, dec: .1f),
                    N(3250, 1900, 1.5f, 0, noise: .35f, noiseBw: .25f, buzz: .5f, am: 35, amDepth: .5f, atk: .02f, dec: .6f, curve: .8f))),
                songShare: 0, perMin: .45f, period: P(0, .5f, 1.3f, 0.96f, 1, .3f), rain: 0, after: 1, fairOnly: true, rest: false, flight: true,
                answer: .1f, minGap: 20, range: 120, near: 12, vol: .6f));

            // 'Who cooks for you, who cooks for you-all': eight hoots, the last drawn out and falling.
            Add("barred_owl", "Its call is the familiar who-cooks-for-you, who-cooks-for-you-all.", V(
                song: C("who-cooks-for-you", 16000, S(
                    Hoot(.17f, .13f), Hoot(.17f, .22f), Hoot(.17f, .13f), Hoot(.20f, .55f),
                    Hoot(.17f, .13f), Hoot(.17f, .22f), Hoot(.17f, .13f),
                    N(570, 380, .95f, 0, vib: 5.5f, vibDepth: .02f, buzz: .25f, atk: .04f, dec: .5f))),
                call: C("hoo-hoo", 16000, S(Hoot(.2f, .2f), Hoot(.2f, .2f), N(560, 400, .5f, 0, buzz: .25f, atk: .04f, dec: .5f))),
                songShare: .65f, perMin: .9f, period: P(1.0f, .3f, 0, 0, .1f, 1.2f), rain: 0, after: .6f,
                answer: .55f, minGap: 20, range: 120, near: 14, vol: .7f));

            Add("wild_turkey", "Hens give soft clucks and rising, whining yelps.", V(
                song: C("yelps", 16000, Rep(3, S(N(900, 1450, .17f, .02f, buzz: .8f, noise: .15f, noiseBw: .3f, dec: .3f), N(1450, 1000, .12f, .16f, buzz: .7f, noise: .15f, dec: .5f)))),
                call: C("clucks", 16000, Rep(3, N(700, 550, .05f, .11f, buzz: .9f, noise: .3f, noiseBw: .4f, dec: 1.2f))),
                songShare: .4f, perMin: .6f, period: P(0, 2, 1.5f, 0.48f, 1, .4f), rain: .1f, after: 1.5f,
                answer: .5f, minGap: 8, range: 70, near: 4, vol: .5f));

            Add("great_blue_heron", "It gives a hoarse, croaking fraaank, now and then.", V(
                call: C("fraank", 16000, N(400, 260, .7f, 0, buzz: .9f, noise: .4f, noiseBw: .3f, am: 70, amDepth: .6f, vib: 6, vibDepth: .02f, atk: .03f, dec: .4f)),
                songShare: 0, perMin: .12f, period: P(0, 2, 1, 0.36f, .8f, 1.5f), rain: .1f, after: 1, rest: true, flight: true,
                answer: .2f, minGap: 40, range: 90, near: 8, vol: .55f));

            // Mammals: nearly all silent. Their only voices are alarms, given when they first notice you.
            Add("white_tailed_deer", "A startled deer blows a sharp, breathy snort.", V(
                alarm: C("snort", 22050, S(N(2600, 900, .38f, .17f, noise: 1f, noiseBw: .5f, atk: .02f, dec: 1.5f), N(2200, 900, .26f, 0, noise: 1f, noiseBw: .5f, amp: .7f, atk: .02f, dec: 1.5f))),
                songShare: 0, perMin: 0, alarmChance: .8f, alarmCd: 25, range: 90, near: 8, vol: .6f));

            Add("eastern_gray_squirrel", "It scolds in a stuttering kuk-kuk-kuk that ends in a long, nasal quaa.", V(
                call: C("kuk", 16000, Rep(3, N(1500, 900, .055f, .08f, noise: .45f, noiseBw: .4f, buzz: .6f, dec: 2f))),
                alarm: C("kuk-quaa", 16000, S(Rep(4, N(1500, 900, .055f, .07f, noise: .45f, noiseBw: .4f, buzz: .6f, dec: 2f)),
                    N(1100, 700, .45f, 0, noise: .5f, noiseBw: .3f, am: 45, amDepth: .7f, buzz: .8f, atk: .02f, dec: .5f))),
                songShare: 0, perMin: .25f, period: P(0, 1, 1.5f, 0.3f, 1.3f, .3f), rain: .05f, after: 1.4f,
                alarmChance: .7f, alarmCd: 30, answer: .3f, minGap: 15, range: 55, near: 3, vol: .5f));

            // The coyote: one long rising howl with a wavering plateau, then yips. Iconic, distant, rare, at dusk and night.
            Add("coyote", "At dusk its howl rises long and thin, and breaks into yips.", V(
                song: C("howl", 16000, S(
                    N(520, 560, 3.0f, .35f, fm: 880, mAt: .38f, vib: 5.5f, vibDepth: .018f, buzz: .8f, noise: .06f, noiseBw: .1f, atk: .25f, decay: .3f),
                    Rep(6, N(950, 1350, .075f, .05f, buzz: .7f, noise: .12f, noiseBw: .2f, dec: 1f)))),
                songShare: 1f, perMin: .25f, period: P(1.2f, .6f, 0, 0, .15f, 1.5f), rain: 0, after: .3f,
                answer: .6f, minGap: 40, range: 120, near: 20, vol: .55f));

            Add("groundhog", "Its alarm is a short, sharp whistle, which is how it got its other name.", V(
                alarm: C("whistle", 22050, S(N(2600, 3050, .42f, .12f, vib: 18, vibDepth: .008f, dec: .3f), N(2700, 3000, .2f, 0, dec: .3f))),
                songShare: 0, perMin: 0, alarmChance: .6f, alarmCd: 40, range: 60, near: 5, vol: .5f));

            // Silent: the red fox, cottontail, raccoon, opossum and black bear. Their sounds (a fox's scream, a bear's huff) are either
            // startling or impossible to make recognisable from code, and in a quiet game their silence is true to how you meet them.
        }

        private static void SmallLife()
        {
            // Frogs call by their own conditions, in a chorus: a call from one invites its neighbours (VoiceRecipe.answer).
            AddSubject("green_frog", V(
                call: C("gunk", 16000, N(420, 290, .30f, 0, buzz: .9f, atk: .004f, dec: 3.5f, curve: .5f)),
                songShare: 0, perMin: 1.4f, period: P(1, .6f, .3f, .2f, .6f, 1.2f), rain: .5f, after: 1, wet: 1.6f,
                answer: .6f, minGap: 3, range: 45, near: 3, vol: .5f));
            AddSubject("northern_leopard_frog", V(
                call: C("snore", 16000, N(420, 330, .75f, 0, buzz: .8f, noise: .2f, noiseBw: .3f, am: 24, amDepth: 1f, atk: .04f, dec: .2f)),
                songShare: 0, perMin: 1.0f, period: P(.8f, 1, .4f, .2f, .5f, 1), rain: .5f, after: 1, wet: 2f,
                answer: .5f, minGap: 4, range: 45, near: 3, vol: .5f));
            AddSubject("gray_treefrog", V(
                call: C("trill", 22050, N(1700, 1900, .75f, 0, buzz: .55f, am: 21, amDepth: 1f, atk: .02f, dec: .2f)),
                songShare: 0, perMin: 3.0f, period: P(1.4f, .6f, .1f, 0, .3f, 1.4f), rain: .8f, after: 1, wet: 1, needsWetNight: true,
                answer: .6f, minGap: 3, range: 55, near: 3, vol: .45f));
        }

        // ------------------------------------------------------------------ builders

        private static VoiceNote Hoot(double dur, double gap) => N(570, 550, dur, gap, vib: 6f, vibDepth: .01f, buzz: .25f, atk: .03f, dec: .15f);

        private static VoiceNote[] Twitter()
        {
            // A canary-like run of quick up-and-down sweeps between 3.5 and 6 kHz.
            var l = new List<VoiceNote>();
            float[] tops = { 5600, 5000, 5900, 4800, 5500, 5200, 5800, 4900, 5400, 5000 };
            for (int i = 0; i < tops.Length; i++)
                l.Add(i % 2 == 0 ? N(3700, tops[i], .07f, .02f, dec: .3f) : N(tops[i], 3800, .07f, .02f, dec: .3f));
            return l.ToArray();
        }

        private static float[] P(double night, double dawn, double morning, double midday, double golden, double dusk) =>
            new[] { (float)night, (float)dawn, (float)morning, (float)midday, (float)golden, (float)dusk };

        private static VoiceNote N(double f0, double f1, double dur, double gap, double amp = 1, double fm = 0, double mAt = .5, double vib = 0, double vibDepth = 0,
            double buzz = 0, double noise = 0, double noiseBw = .15, double am = 0, double amDepth = 0, double atk = .01, double dec = .35, double decay = -1, double curve = 1)
        {
            return new VoiceNote
            {
                f0 = (float)f0, f1 = (float)f1, fm = (float)fm, mAt = (float)mAt, dur = (float)dur, gap = (float)gap, amp = (float)amp, vib = (float)vib, vibDepth = (float)vibDepth,
                buzz = (float)buzz, noise = (float)noise, noiseBw = (float)noiseBw, am = (float)am, amDepth = (float)amDepth, atk = (float)atk,
                decay = (float)(decay >= 0 ? decay : dec), curve = (float)curve,
            };
        }

        private static VoiceNote[] Rep(int n, params object[] parts)
        {
            var one = S(parts);
            var all = new VoiceNote[one.Length * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < one.Length; j++) all[i * one.Length + j] = Clone(one[j]);
            return all;
        }

        private static VoiceNote[] S(params object[] parts)
        {
            var l = new List<VoiceNote>();
            foreach (var p in parts)
            {
                var one = p as VoiceNote;
                if (one != null) l.Add(one); else l.AddRange((VoiceNote[])p);
            }
            return l.ToArray();
        }

        private static VoiceCall C(string name, int rate, object notes) =>
            new VoiceCall { name = name, rate = rate, notes = notes as VoiceNote[] ?? new[] { (VoiceNote)notes } };

        /// <summary>Recipes are written in natural call rates (per minute, per bird); this scales them all for a calm game, in one place.</summary>
        private const double Sparse = 0.4;

        private static VoiceRecipe V(VoiceCall song = null, VoiceCall call = null, VoiceCall alarm = null, double songShare = .6, double perMin = 1,
            float[] period = null, double rain = .05, double after = 1, double wet = 1, bool fairOnly = false, bool needsWetNight = false,
            bool rest = true, bool flight = false, bool aware = false, double alarmChance = 0, double alarmCd = 40, double answer = .3,
            double minGap = 6, double range = 70, double near = 4, double vol = .5)
        {
            return new VoiceRecipe
            {
                enabled = true,
                song = song ?? new VoiceCall { name = "song" }, call = call ?? new VoiceCall { name = "call" }, alarm = alarm ?? new VoiceCall { name = "alarm" },
                songShare = (float)songShare, perMinute = (float)(perMin * Sparse), period = period ?? P(0, 1, 1, 1, 1, 1), rain = (float)rain, afterRain = (float)after, wet = (float)wet,
                fairOnly = fairOnly, needsWetNight = needsWetNight, atRest = rest, inFlight = flight, awareOk = aware,
                alarmChance = (float)alarmChance, alarmCooldown = (float)alarmCd, answer = (float)answer, minGap = (float)minGap, range = (float)range, near = (float)near, volume = (float)vol,
            };
        }

        private static void Add(string id, string note, VoiceRecipe r) { _species[id] = r; _notes[id] = note; }
        private static void AddSubject(string id, VoiceRecipe r) { _subjects[id] = r; }

        private static VoiceNote Clone(VoiceNote n) => (VoiceNote)CloneObj(n);

        private static object CloneObj(object o)
        {
            var n = o as VoiceNote;
            return new VoiceNote
            {
                f0 = n.f0, f1 = n.f1, fm = n.fm, mAt = n.mAt, dur = n.dur, gap = n.gap, amp = n.amp, vib = n.vib, vibDepth = n.vibDepth, buzz = n.buzz,
                noise = n.noise, noiseBw = n.noiseBw, am = n.am, amDepth = n.amDepth, atk = n.atk, decay = n.decay, curve = n.curve,
            };
        }

        private static VoiceCall Clone(VoiceCall c)
        {
            if (c == null) return null;
            var notes = new VoiceNote[c.notes != null ? c.notes.Length : 0];
            for (int i = 0; i < notes.Length; i++) notes[i] = Clone(c.notes[i]);
            return new VoiceCall { name = c.name, rate = c.rate, notes = notes };
        }

        /// <summary>A deep copy (so an asset's voice is its own and can be tuned in the Inspector without touching the defaults).</summary>
        public static VoiceRecipe Clone(VoiceRecipe r)
        {
            return new VoiceRecipe
            {
                enabled = r.enabled, song = Clone(r.song), call = Clone(r.call), alarm = Clone(r.alarm), songShare = r.songShare, perMinute = r.perMinute,
                period = r.period != null ? (float[])r.period.Clone() : null, rain = r.rain, afterRain = r.afterRain, wet = r.wet, fairOnly = r.fairOnly,
                needsWetNight = r.needsWetNight, atRest = r.atRest, inFlight = r.inFlight, awareOk = r.awareOk, alarmChance = r.alarmChance,
                alarmCooldown = r.alarmCooldown, answer = r.answer, minGap = r.minGap, range = r.range, near = r.near, volume = r.volume,
            };
        }
    }
}
