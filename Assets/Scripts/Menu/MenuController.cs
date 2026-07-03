using System;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using Random = UnityEngine.Random;

public enum GameScreens {Engagement = 0, User = 1, Lobby = 2, Host = 3, Join = 4, Room = 5}

public class MenuController : MonoBehaviourPunCallbacks
{
    [SerializeField]
    GameObject menuSplashScreen;
    
    [SerializeField]
    GameObject engagementScreen, userScreen, lobbyScreen, hostScreen, joinScreen, roomScreen, loadingScreen;
    
    [SerializeField]
    TMP_Text roomTitle, joinMessage;
    
    [SerializeField]
    TMP_InputField usernameInput, roomNameInput, joinRoomInput;
    
    [SerializeField]
    GameObject startGameButton;
    
    private void Start()
    {
        OpenScreen(0);
    }

    public override void OnConnected()
    {
        Debug.Log("Connected");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected To Master");
        CloseAllScreens();
        userScreen.SetActive(true);
        
        PhotonNetwork.AutomaticallySyncScene = true; // Assegura que as mudanças de cena sejam replicadas para todos os jogadores conectados a uma sala.
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        OpenScreen(2);
    }
    
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined Room");
        OpenScreen(5);
        roomTitle.text = PhotonNetwork.CurrentRoom.Name;
        UpdatePlayers();
    }

    private void UpdatePlayers() //Atualiza a lista de jogadores na sala, é chamado sempre que um jogador entra ou sai da sala.
    {
       return;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayers();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayers();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        startGameButton.SetActive(PhotonNetwork.IsMasterClient); // Assegura que, caso o host saia da sala, o novo host tenha acesso às funções exclusivas do host, que no momento inclui apenas a opção de iniciar o jogo.
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join room: " + message);
        OpenScreen((int)GameScreens.Join);
        joinMessage.text = "Sala inexistente ou cheia. Verifique o nome e tente novamente.";
    }

    public void OpenScreen(int screen) //Função para controlar a navegação entre as telas do menu, sempre garantindo que as telas não se sobreponham, através da função "CloseAllScreens()"
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
            
            case GameScreens.Room:
                roomScreen.SetActive(true);
                startGameButton.SetActive(PhotonNetwork.IsMasterClient); //Caso o jogador seja o host, ativa a opção de iniciar o jogo.
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
        roomScreen.SetActive(false);
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
        if (tempUsername == "")
        {
            tempUsername = "Jogador_" + Random.Range(1, 99); //Garante que os jogadores sempre tenham um nome, mesmo que não tenham digitado um nick.
        }
        
        PhotonNetwork.NickName = tempUsername;
    }

    public void CreateRoom()
    {
        if (roomNameInput.text == "")
        {
            roomNameInput.text = "Sala " + Random.Range(1, 99); //Garante que a sala criada sempre tenha um nome, mesmo que o host não tenha digitado um nome para a sala.
        }
        else
        {
            RoomOptions roomOptions = new RoomOptions();
            roomOptions.MaxPlayers = 4;
            
            ShowLoadingScreen();
            PhotonNetwork.CreateRoom(roomNameInput.text, roomOptions); // Cria a sala usando o nome especificado e as opções definidas.
        }
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

    public void StartGame()
    {
        ShowLoadingScreen();
        PhotonNetwork.LoadLevel(1);
    }

    public void LeaveRoom()
    {
        ShowLoadingScreen();
        PhotonNetwork.LeaveRoom();
    }

    public void QuitApp()
    {
        Application.Quit();
    }
    
}