using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KModkit;
using Newtonsoft.Json;
using Rnd = UnityEngine.Random;



public class colorPong_Script : MonoBehaviour
{

    public class ModSettingsJSON
    {
        public float TimeLimit;
        public int _stageCount;
    }


    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBombModule Module;
    public KMColorblindMode Colorblind;
    public KMModSettings modSettings;

    public KMSelectable[] Buttons;
    public TextMesh Display;
    public TextMesh ColorblindText;
    public MeshRenderer Ball;

    private float _time;
    private int _stageCount;
    private bool _isRunning;
    private bool _hasPressed;
    private bool _lastPressRight, _currentPressRight, _initialPressRight;
    private int _initialColor;
    float TimeLimit()
    {
        try
        {
            ModSettingsJSON settings = JsonConvert.DeserializeObject<ModSettingsJSON>(modSettings.Settings);
            if (settings != null)
            {
                if (settings.TimeLimit < 1)
                    return 1;
                else if (settings.TimeLimit > 60)
                    return 60;
                else return settings.TimeLimit;
            }
            else return 5;
        }
        catch (JsonReaderException e)
        {
            Debug.LogFormat("[color pong #{0}] JSON reading failed with error time, using default threshold.", _moduleID);
            return 5;
        }
    }
    int _initialStageCount()
    {
        try
        {
            ModSettingsJSON settings = JsonConvert.DeserializeObject<ModSettingsJSON>(modSettings.Settings);
            if (settings != null)
            {
                if (settings._stageCount < 1)
                    return 1;
                
                else return settings._stageCount;
            }
            else return 10;
        }
        catch (JsonReaderException e)
        {
            Debug.LogFormat("[color pong #{0}] JSON reading failed with error time, using default threshold.", _moduleID);
            return 10;
        }
    }
    bool TwitchPlaysActive;
    private bool _colorblindActive;
    
    private static readonly Color[] Colors =
        {
            new Color(1,0,0), //red
            new Color(1,0.5f,0), //orange
            new Color(0,0.75f,0), //green
            new Color(0,0.75f,1), //cyan
            new Color(0,0,1), //blue
            new Color(0.75f,0,1), //purple
            new Color(1,0.375f,0.75f), //pink
            new Color(0.75f,0.75f,0.75f) //grey
        };
    private static readonly string[] ColorNames =
        {
            "red",
            "orange",
            "green",
            "cyan",
            "blue",
            "purple",
            "pink",
            "grey"
        };
    private static readonly string[] ColorN =
        {
            "R",
            "O",
            "G",
            "C",
            "B",
            "P",
            "I",
            "X"
        };

    private bool _overshoot, _leftHanded;
    
    private bool _solved = false;
    static int _moduleIdCounter = 1;
    int _moduleID = 0;
    
    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate
            {
                Press(x);
                return false;
            };
        }
    }

    // Use this for initialization
    void Start()
    {
        _colorblindActive = Colorblind.ColorblindModeActive;
        GetEdgework();
        _stageCount = _initialStageCount();
        Initialize();

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void Initialize()
    {
        _isRunning = false;
        _initialColor = Rnd.Range(0, 7); // select a random initial color
        Ball.material.color = Colors[_initialColor]; // set the ball material as the initial color
        SetColorblindColor(_initialColor);
        Display.text = "--.--";
        Log("The initial color for the next set is {0}.", ColorNames[_initialColor]);
        Log("initial stage Count is {0}and curent stage in {1}.", _initialStageCount(), _stageCount );
        
    }

    private void GetEdgework()
    {
        _overshoot = Bomb.GetBatteryCount() > 5; // if there are more than 5 batteries the opponent may overshoot
        _leftHanded = Bomb.GetSerialNumber().Contains("L"); // if there is an L in the serial number you are left handed
        if (_overshoot)
        {
            Log("There are more than 5 batteries. The opponent may overshoot.");
        }
        if (_leftHanded)
        {
            Log("There is an L in the serial number. You are left handed.");
        }
    }

    private void Press(int index) // detect if a press as occurre and start a play set
    {
        Buttons[index].AddInteractionPunch();
        _lastPressRight = _currentPressRight;
        _currentPressRight = index == 1;
        _hasPressed = true;
        Audio.PlaySoundAtTransform(_currentPressRight ^ _leftHanded ? "ping" : "pong", Buttons[index].transform);
        if (!_isRunning)
        {
            _initialPressRight = _currentPressRight;
            StartCoroutine(PlaySet()); 
        }
        //Debug.Log(index);
    }

    private void SetDisplay(float value)
    {
        Display.text = ((int)value).ToString("00") + "." + ((int)(value * 100) % 100).ToString("00");
    }

    private bool CorrectResponse(int color) // test if the response is correct
    {
        switch (color)
        {
            case 0:  //red
                return _hasPressed && (!_currentPressRight ^ _leftHanded);

            case 1:  //orange
                return _hasPressed && (_currentPressRight == _lastPressRight);

            case 2:  //green
                return _hasPressed && (_currentPressRight ^ _leftHanded);

            case 3:  //cyan
                return !_hasPressed;

            case 4:  //blue
                return _hasPressed && (_currentPressRight == _initialPressRight);

            case 5:  //purple
                return _hasPressed && (_currentPressRight != _lastPressRight);

            case 6:  //pink
                return _hasPressed;

            case 7:  //grey
                return CorrectResponse(_initialColor);
        }
        return false;
    }

    private IEnumerator PlaySet() // game loop
    {
        int previousColor = _initialColor;
        

        _isRunning = true;
        do
        {
            int currentColor = Rnd.Range(0, 8); // select a random current color
            Ball.material.color = Colors[currentColor]; // set the ball material as the current color
            SetColorblindColor(currentColor);
            Log("The next color is {0}.", ColorNames[currentColor]);

            _hasPressed = false;
            _time = TimeLimit() + (TwitchPlaysActive ? 10 : 0); // set time to the time limit and if TwitchPlays is active add 10 to it
            while (_time > 0 && !_hasPressed) // while the module is not pressed the time will decrese by 1 sec etch sec 
            {
                SetDisplay(_time);
                yield return null;
                _time -= Time.deltaTime;
            }

            bool correct;
            if (_overshoot && currentColor == _initialColor)
            {
                correct = !_hasPressed;
                Log("The opponent overshot.");
            }
            else
            {
                correct = CorrectResponse(previousColor);
            }

            Log("You pressed {0}, which is {1}correct.", _hasPressed ? (_currentPressRight ? "right" : "left") : "nothing", correct ? "" : "in");
            if (correct)
            {
                _stageCount--;
                previousColor = currentColor;               
            }
            else
            {
                ColorblindText.text = "";
                Module.HandleStrike();
                _hasPressed = false;
                for (int i = 0; i < 5; i++)
                {
                    Ball.material.color = new Color(i % 2 == 0 ? 0.75f : 0, 0, 0);
                    yield return new WaitForSeconds(0.1f);
                }
            }

        } while (_hasPressed && _stageCount > 0);
        if (_stageCount == 0)
        {
            ColorblindText.text = "";
            Log("Your opponent has been defeated!");
            _solved = true;
            Module.HandlePass();           
            Display.text = "DONE";
            Ball.material.color = new Color(0.25f, 1, 0.25f);
        }
        else
        {
            Initialize();
        }
        yield return null;
    }

    private void ToggleColorblind()
    {
        _colorblindActive = !_colorblindActive;
        if (!_isRunning)
        {
            SetColorblindColor(_initialColor);
        }
    }

    private void SetColorblindColor(int color)
    {
        if (_colorblindActive)
        {
            ColorblindText.text = ColorN[color];
        }
        else
        {
            ColorblindText.text = "";
        }
    }

    private void Log(string message, params object[] args)
    {
        Debug.LogFormat("[Color Pong #{0}] {1}", _moduleID, string.Format(message, args));
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "'!{0} l/r' to press left or right. '!{0} colorblind' to toggle colorblind mode.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        yield return null;
        command = command.ToLowerInvariant();

        if (command == "colorblind")
        {
            ToggleColorblind();
        }

        else if (command.Length == 1 && "lr".Contains(command))
        {
            Buttons["lr".IndexOf(command[0])].OnInteract();
        }

        else
        {
            yield return "sendtochaterror Invalid command.";
            yield break;
        }
    }
}
