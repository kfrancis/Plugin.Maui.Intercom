﻿using Plugin.Maui.Intercom;

namespace MauiSample;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();
    }
}
