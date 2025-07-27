using UnityEngine;
using UnityEngine.SceneManagement;
public class PauseMenu : MonoBehaviour
{
    

    [SerializeField] GameObject pauseMenu;
    

    public void PauseGame()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0;
        
    }
    public void ResumeGame()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1;
        
    }
    public void MainMenu()
    {
        SceneManager.LoadScene(0);
        Time.timeScale = 1;
    }
    public void RestartGame()
    {
        SceneManager.LoadScene(gameObject.scene.name);
        Time.timeScale = 1;
    }

}
