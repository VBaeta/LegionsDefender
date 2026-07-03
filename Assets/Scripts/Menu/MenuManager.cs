using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using Random = UnityEngine.Random;

public enum GameScreens { Engagement = 0, User = 1, Lobby = 2, Host = 3, Join = 4 }

public class MenuManager : MonoBehaviourPunCallbacks
{
    public static MenuManager Instance { get; private set; }

    [SerializeField]
    GameObject engagementScreen, userScreen, lobbyScreen, hostScreen, joinScreen, loadingScreen;
    
    [SerializeField]
    TMP_Text joinMessage;
    
    [SerializeField]
    TMP_InputField usernameInput, roomNameInput, joinRoomInput;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            if (PhotonNetwork.InLobby)
            {
                OpenScreen((int)GameScreens.Lobby);
            }
            else
            {
                ShowLoadingScreen();
                PhotonNetwork.JoinLobby();
            }
        }
        else
        {
            OpenScreen(0);
        }
    }

    public override void OnConnected()
    {
        Debug.Log("Connected to Photon");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected To Master");
        CloseAllScreens();
        userScreen.SetActive(true);
        
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        OpenScreen(2);
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room");
        ShowLoadingScreen();
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("RoomScene");
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join room: " + message);
        OpenScreen((int)GameScreens.Join);
        joinMessage.text = "Sala inexistente ou cheia. Verifique o nome e tente novamente.";
    }

    public void OpenScreen(int screen)
    {
        CloseAllScreens();
        
        switch ((GameScreens)screen)
        {
            case GameScreens.Engagement:
                engagementScreen.SetActive(true);
                break;
            
            case GameScreens.User:
                ShowLoadingScreen();
                PhotonNetwork.ConnectUsingSettings();
                break;
            
            case GameScreens.Lobby:
                lobbyScreen.SetActive(true);
                break;
            
            case GameScreens.Host:
                hostScreen.SetActive(true);
                break;
            
            case GameScreens.Join:
                joinScreen.SetActive(true);
                break;
        }
    }

    public void CloseAllScreens()
    {
        engagementScreen.SetActive(false);
        userScreen.SetActive(false);
        lobbyScreen.SetActive(false);
        hostScreen.SetActive(false);
        joinScreen.SetActive(false);
        loadingScreen.SetActive(false);
        joinMessage.text = "";
    }

    public void ShowLoadingScreen()
    {
        CloseAllScreens();
        loadingScreen.SetActive(true);
    }

    public void Connect()
    {
        SaveUsername();
        ShowLoadingScreen();
        PhotonNetwork.JoinLobby();
    }

    void SaveUsername()
    {
        string tempUsername = usernameInput.text;
        if (string.IsNullOrEmpty(tempUsername))
        {
            tempUsername = "Jogador_" + Random.Range(1, 99);
        }
        
        PhotonNetwork.NickName = tempUsername;
    }

    public void CreateRoom()
    {
        if (roomNameInput.text == "")
        {
            roomNameInput.text = "Sala " + Random.Range(1, 99);
        }
        
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 4;
        
        ShowLoadingScreen();
        PhotonNetwork.CreateRoom(roomNameInput.text, roomOptions);
    }

    public void JoinRoom()
    {
        if (string.IsNullOrEmpty(joinRoomInput.text))
        {
            joinMessage.text = "Insira o nome da sala que deseja entrar. Atente-se à espaços e letras maiúsculas.";
            return;
        }
        
        Debug.Log("Joining Room: " + joinRoomInput.text);
        ShowLoadingScreen();
        PhotonNetwork.JoinRoom(joinRoomInput.text);
    }

    public void QuitApp()
    {
        Application.Quit();
    }
}
