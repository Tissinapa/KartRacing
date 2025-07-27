using UnityEngine;
using UnityEngine.SceneManagement;

public class TracksMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadSceneAsync(2);
    }
}
