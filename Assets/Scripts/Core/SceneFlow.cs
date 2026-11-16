using UnityEngine;
using UnityEngine.SceneManagement;

namespace Oncologie.Core
{
    /// <summary>
    /// Wisselt tussen de IRL-keukenscène en de darmscène (scene loads i.p.v. teleporteren).
    /// De keuze van de speler wordt in een statische sessie bewaard zodat ze een scene-load overleeft.
    ///
    /// De darmscène zelf hoeft niets te weten van dit script: bij het laden van de darmscène
    /// wordt ColonPathRider.onReachedEnd automatisch gekoppeld aan "terug naar IRL".
    /// Zo hoeft niemand de darmscène aan te passen om de flow te laten werken.
    /// </summary>
    public static class SceneFlow
    {
        // Moeten overeenkomen met de scènenamen in Build Settings.
        public const string IrlScene = "IRL_Kitchen";
        public const string ColonScene = "DevScene - Lowie";

        /// <summary>Keuze van de speler in de keuken, blijft bewaard over scene-loads heen.</summary>
        public static ScreeningChoice PlayerChoice { get; private set; } = ScreeningChoice.None;

        /// <summary>True wanneer de IRL-scène geladen wordt na de darmrit (dan start de outro i.p.v. de intro).</summary>
        public static bool ReturningFromColon { get; private set; }

        private static bool _loading;
        private static bool _enteredFromIrl;

        public static void LoadColon(ScreeningChoice choice)
        {
            PlayerChoice = choice;
            ReturningFromColon = false;
            _enteredFromIrl = true;
            Load(ColonScene);
        }

        public static void ReturnToIrl()
        {
            ReturningFromColon = true;
            Load(IrlScene);
        }

        /// <summary>Herlaadt de IRL-scène (bv. na "tijd terugdraaien").</summary>
        public static void ReloadIrl() => Load(IrlScene);

        /// <summary>Test-snelkoppeling: laad meteen het einde voor een bepaalde keuze.</summary>
        public static void LoadOutro(ScreeningChoice choice)
        {
            PlayerChoice = choice;
            ReturnToIrl();
        }

        /// <summary>Volledige reset: volgende IRL-load start terug bij de intro.</summary>
        public static void ResetSession()
        {
            PlayerChoice = ScreeningChoice.None;
            ReturningFromColon = false;
        }

        /// <summary>Wordt door de IRL-scène aangeroepen zodra de terugkeer verwerkt is.</summary>
        public static void ConsumeReturn() => ReturningFromColon = false;

        private static void Load(string sceneName)
        {
            if (_loading) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"SceneFlow: scène '{sceneName}' staat niet in Build Settings.");
                return;
            }
            _loading = true;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Nodig wanneer Domain Reload uit staat in de Editor.
            PlayerChoice = ScreeningChoice.None;
            ReturningFromColon = false;
            _loading = false;
            _enteredFromIrl = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _loading = false;
            // Alleen koppelen als we vanuit de keuken kwamen; wie de darmscène los test, blijft daar.
            if (scene.name != ColonScene || !_enteredFromIrl) return;
            _enteredFromIrl = false;

            var rider = Object.FindAnyObjectByType<ColonPathRider>();
            if (rider == null)
            {
                Debug.LogWarning("SceneFlow: geen ColonPathRider gevonden in de darmscène; terugkeer naar IRL is niet gekoppeld.");
                return;
            }
            rider.onReachedEnd.AddListener(ReturnToIrl);

            // Zachte aankomst: vanuit zwart infaden, zodat de overgang uit de keuken doorloopt.
            var fader = Oncologie.IRL.ScreenFader.Instance;
            fader.SetImmediate(1f);
            fader.StartCoroutine(fader.Fade(0f, 2.5f));
        }
    }
}
