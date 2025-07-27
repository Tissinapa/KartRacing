using UnityEngine;
using UnityEngine.SceneManagement;

public class BackMenu : MonoBehaviour
{
    public void PlayGame()
    {
        // Back to main menu
        SceneManager.LoadSceneAsync(0);
    }
}
