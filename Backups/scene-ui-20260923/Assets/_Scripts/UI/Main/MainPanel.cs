using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainPanel : MonoBehaviour
{
    private Button btnStart;
    private Button btnQuit;


    private void Awake()
    {
        btnStart=transform.Find("btnStart").GetComponent<Button>();
        btnQuit=transform.Find("btnQuit").GetComponent<Button>();
    }

    private void OnEnable()
    {
        btnStart.onClick.AddListener(Start);
        btnQuit.onClick.AddListener(Start);
    }

    private void OnDisable()
    {
        btnStart.onClick.AddListener(Start);
        btnQuit.onClick.AddListener(Quit);
    }

    private void Quit()
    {
        Application.Quit();
    }

    void Start()
    {
        SceneManager.LoadScene("SelectScene");
    }
}