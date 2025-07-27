using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;




[RequireComponent(typeof(Button))]
public class BackToMainMenu : MonoBehaviour
{
    
    Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        SceneManager.LoadSceneAsync(0);
    }
}

