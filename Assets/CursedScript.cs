using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using UnityEngine.UI;

public class CursedScript : MonoBehaviour {

    public KMAudio audio;
    public KMBombInfo bomb;
    public KMSelectable module;
    public TextMesh curseText;
    public GameObject curseOfTheBlind;
    public GameObject curseOfDarkness;

    private static List<int> availableCurses = new List<int>() { 0, 1, 2, 3 };
    private string[] curseNames = { "Curse of\nthe Blind!", "Curse of\nDarkness!", "Curse of\nthe Unknown!", "Curse of\nthe Maze!" };
    private int chosenCurse;

    private Text tweaksStrikeText;
    private Text tweaksStrikeTextClone;

    private static Dictionary<string, Transform[]> blindDict;
    private string[] vanillaNames = { "WireSequenceComponent(Clone)", "WireSetComponent(Clone)", "WhosOnFirstComponent(Clone)", "NeedyVentComponent(Clone)", "SimonComponent(Clone)", "PasswordComponent(Clone)", "MorseComponent(Clone)", "MemoryComponent(Clone)", "InvisibleWallsComponent(Clone)", "NeedyKnobComponent(Clone)", "KeypadComponent(Clone)", "VennWiresComponent(Clone)", "ButtonComponent(Clone)", "NeedyDischargeComponent(Clone)" };
    private string hoveredCollider = null;

    private static Type selectableType = ReflectionHelper.FindType("Selectable", "Assembly-CSharp");

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        module.OnInteract += delegate () { PressModule(); return false; };
        GetComponent<KMBombModule>().OnActivate += Activate;
    }

    void Start()
    {
        string missionDesc = KTMissionGetter.Mission.Description;
        if (missionDesc != null)
        {
            Regex regex = new Regex(@"\[Cursed\] (blind|dark|unknown|maze)(, *(blind|dark|unknown|maze))?(, *(blind|dark|unknown|maze))?(, *(blind|dark|unknown|maze))?");
            var match = regex.Match(missionDesc);
            if (match.Success)
            {
                string[] options = match.Value.Replace("[Cursed] ", "").Split(',');
                for (int i = 0; i < options.Length; i++)
                    options[i] = options[i].Trim();
                if (!options.Contains("blind"))
                    availableCurses.Remove(0);
                if (!options.Contains("dark"))
                    availableCurses.Remove(1);
                if (!options.Contains("unknown"))
                    availableCurses.Remove(2);
                if (!options.Contains("maze"))
                    availableCurses.Remove(3);
            }
        }
        if (availableCurses.Contains(3))
        {
            var modules = bomb.GetModuleNames();
            var ignored = GetComponent<KMBossModule>().GetIgnoredModules("Cursed", new string[] { "Hickory Dickory Dock" });
            if (ignored.Any(modules.Contains))
                availableCurses.Remove(3);
        }
        if (availableCurses.Count > 0)
        {
            chosenCurse = availableCurses.PickRandom();
            availableCurses.Remove(chosenCurse);
            Debug.LogFormat("[Cursed #{0}] This bomb has been cursed with \"{1}\"", moduleId, curseNames[chosenCurse].Replace("\n", " ").Replace("!", ""));
        }
        else
        {
            chosenCurse = -1;
            Debug.LogFormat("[Cursed #{0}] Failed to apply any curse as all curses have already been applied", moduleId);
        }

        if (chosenCurse != -1)
            curseText.text = curseNames[chosenCurse];
        else
            curseText.text = "Curse of\nNothing!";

        if (chosenCurse == 3)
        {
            Debug.LogFormat("<Cursed #{0}> Starting position swapping timer", moduleId);
            StartCoroutine(CurseMazeTimer());
        }
        else if (chosenCurse == 2)
        {
            Debug.LogFormat("<Cursed #{0}> Attempting to obscure the Tweaks UI strike text...", moduleId);
            try
            {
                tweaksStrikeText = GameObject.Find("ModManager(Clone)/Tweaks(Clone)/UI/BombStatus/HUD/StatsGroup/Strikes").GetComponent<Text>();
                tweaksStrikeTextClone = Instantiate(tweaksStrikeText, tweaksStrikeText.transform.parent);
                tweaksStrikeTextClone.transform.localPosition = tweaksStrikeText.transform.localPosition;
                tweaksStrikeTextClone.transform.SetAsFirstSibling();
                tweaksStrikeText.gameObject.SetActive(false);
                tweaksStrikeTextClone.text = "?<size=25>/?</size>";
                Debug.LogFormat("<Cursed #{0}> Successfully obscured the Tweaks UI strike text", moduleId);
            }
            catch (NullReferenceException)
            {
                Debug.LogFormat("<Cursed #{0}> Failed to obscure the Tweaks UI strike text", moduleId);
            }
            Debug.LogFormat("<Cursed #{0}> Attempting to obscure the bomb timer's strike display...", moduleId);
            try
            {
                Transform timer = transform.parent.Find("TimerComponent(Clone)");
                Transform[] strikeObjs = new Transform[4];
                for (int i = 0; i < 4; i++)
                    strikeObjs[i] = timer.GetChild(i);
                GameObject stuffToDisable = new GameObject("DisableStrikeDisplay");
                for (int i = 0; i < 4; i++)
                    strikeObjs[i].parent = stuffToDisable.transform;
                stuffToDisable.transform.parent = timer;
                stuffToDisable.SetActive(false);
                Debug.LogFormat("<Cursed #{0}> Successfully obscured the bomb timer's strike display", moduleId);
            }
            catch (NullReferenceException)
            {
                Debug.LogFormat("<Cursed #{0}> Failed to obscure the bomb timer's strike display", moduleId);
            }
            StartCoroutine(AttemptObscureBTM());
        }
        else if (chosenCurse == 1)
        {
            curseOfDarkness.SetActive(true);
            Debug.LogFormat("<Cursed #{0}> Enabled darkness overlay effect", moduleId);
        }
    }

    void Activate()
    {
        if (chosenCurse == 0)
        {
            StartCoroutine(BlindWithDelay());
        }
    }

    void Update()
    {
        if (chosenCurse == 0)
        {
            RaycastHit[] allHit = Physics.RaycastAll(Camera.main.ScreenPointToRay(Input.mousePosition));
            float dist = 99999999;
            int index = -1;
            for (int i = 0; i < allHit.Length; i++)
            {
                if (allHit[i].collider.name.StartsWith("CurseOfTheBlind") && allHit[i].distance < dist)
                {
                    dist = allHit[i].distance;
                    index = i;
                }
            }
            if (index != -1)
            {
                if (hoveredCollider != null)
                {
                    blindDict[hoveredCollider][0].localScale = new Vector3(0, 0, 0);
                    blindDict[hoveredCollider][1].gameObject.SetActive(true);
                }
                blindDict[allHit[index].collider.name][0].localScale = new Vector3(1, 1, 1);
                blindDict[allHit[index].collider.name][1].gameObject.SetActive(false);
                hoveredCollider = allHit[index].collider.name;
            }
            else if (hoveredCollider != null)
            {
                blindDict[hoveredCollider][0].localScale = new Vector3(0, 0, 0);
                blindDict[hoveredCollider][1].gameObject.SetActive(true);
                hoveredCollider = null;
            }
        }
    }

    void OnDestroy()
    {
        availableCurses = new List<int>() { 0, 1, 2, 3 };
        blindDict = null;
        if (tweaksStrikeTextClone != null)
        {
            Destroy(tweaksStrikeTextClone.gameObject);
            tweaksStrikeText.gameObject.SetActive(true);
        }
    }

    void PressModule()
    {
        if (moduleSolved != true)
        {
            moduleSolved = true;
            GetComponent<KMBombModule>().HandlePass();
            Debug.LogFormat("[Cursed #{0}] Module solved", moduleId);
        }
    }

    IEnumerator AttemptObscureBTM()
    {
        int attempts = 0;
        Debug.LogFormat("<Cursed #{0}> Attempting to obscure the Bomb Timer Modifier strike display...", moduleId);
        tryAgain2:
        yield return null;
        try
        {
            transform.parent.Find("TimerV2(Clone)/Canvas/Strikes").gameObject.SetActive(false);
            Debug.LogFormat("<Cursed #{0}> Successfully obscured the Bomb Timer Modifier strike display", moduleId);
        }
        catch (NullReferenceException)
        {
            if (attempts < 5)
            {
                attempts++;
                goto tryAgain2;
            }
            Debug.LogFormat("<Cursed #{0}> Failed to obscure the Bomb Timer Modifier strike display", moduleId);
        }
    }

    IEnumerator CurseMazeTimer()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(10f, 30f));
            var selectable = transform.GetComponent(selectableType);
            var bombFaces = selectable.GetValue<object>("Parent").GetValue<object>("Parent").GetValue<object[]>("Children");
            var modules = bombFaces[UnityEngine.Random.Range(0, bombFaces.Length)].GetValue<object[]>("Children");
            if (modules.Count(x => x != null) > 1)
            {
                int choice1 = UnityEngine.Random.Range(0, modules.Length);
                int choice2 = UnityEngine.Random.Range(0, modules.Length);
                while (choice1 == choice2 || modules[choice1] == null || modules[choice2] == null)
                {
                    choice1 = UnityEngine.Random.Range(0, modules.Length);
                    choice2 = UnityEngine.Random.Range(0, modules.Length);
                }
                Vector3 pos1Copy = modules[choice1].GetValue<GameObject>("gameObject").transform.localPosition;
                int pos1X = modules[choice1].GetValue<int>("x");
                int pos1Y = modules[choice1].GetValue<int>("y");
                modules[choice1].GetValue<GameObject>("gameObject").transform.localPosition = modules[choice2].GetValue<GameObject>("gameObject").transform.localPosition;
                modules[choice1].SetValue("x", modules[choice2].GetValue<int>("x"));
                modules[choice1].SetValue("y", modules[choice2].GetValue<int>("y"));
                modules[choice2].GetValue<GameObject>("gameObject").transform.localPosition = pos1Copy;
                modules[choice2].SetValue("x", pos1X);
                modules[choice2].SetValue("y", pos1Y);
                if (blindDict != null)
                {
                    for (int i = 0; i < blindDict.Count; i++)
                        blindDict["CurseOfTheBlind" + i][1].parent.localPosition = blindDict["CurseOfTheBlind" + i][0].localPosition;
                }
            }
        }
    }

    IEnumerator BlindWithDelay()
    {
        yield return null;
        blindDict = new Dictionary<string, Transform[]>();
        int ct = 0;
        for (int i = 0; i < transform.parent.childCount; i++)
        {
            Transform componentTransform = transform.parent.GetChild(i);
            if ((componentTransform.GetComponent<KMBombModule>() != null && componentTransform.GetComponent<KMBombModule>().ModuleDisplayName != "Cursed") || componentTransform.GetComponent<KMNeedyModule>() != null || vanillaNames.Contains(componentTransform.gameObject.name))
            {
                GameObject qMark = Instantiate(curseOfTheBlind, componentTransform.parent);
                qMark.transform.localPosition = componentTransform.localPosition;
                qMark.transform.localEulerAngles = componentTransform.localEulerAngles;
                qMark.GetComponent<Collider>().name = "CurseOfTheBlind" + ct;
                blindDict.Add(qMark.GetComponent<Collider>().name, new Transform[] { componentTransform, qMark.transform.GetChild(0) });
                componentTransform.localScale = new Vector3(0, 0, 0);
                ct++;
            }
        }
        Debug.LogFormat("<Cursed #{0}> Added blindness effect to {1} modules", moduleId, ct);
    }
}
