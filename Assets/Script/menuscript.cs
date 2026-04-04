using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu: MonoBehaviour
{
    public void Play()
    {
        SceneManager.LoadScene("Level1");
    }
    public void OpenLevels()
    {
        SceneManager.LoadScene("LevelSelect");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}