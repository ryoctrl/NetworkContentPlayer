using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

public class NicoAPI : MonoBehaviour {
	public static string USER_ID = "";
	public static string PASSWORD = "";

	public static bool userSessionCookieGenerated = false;
	public static string userSession = null;

	public const string NICO_LOGIN_URI = "https://secure.nicovideo.jp/secure/login?site=niconico";
	public const string GETFLV_URI = "http://flapi.nicovideo.jp/api/getflv/";
	public const string HISTORY_URI = "http://www.nicovideo.jp/watch/";

	private string history = null;
	private string contentId = null;

	private string currentCookie = null;

	public IEnumerator GetPathFromDownloadVideoByContentID(string url, Action<string> callback) {
		ParseContentID(url);
		if(userSession == null) {
			yield return StartCoroutine(Login());
		}

		StartCoroutine(CreateHistory());

		string videoURL = "";
		yield return StartCoroutine(GetVideoURL(vURL => videoURL = vURL));

		byte[] videoData = null;
		yield return StartCoroutine(GetVideo(videoURL, data => videoData = data));

		string path = Application.persistentDataPath + "/" + contentId + ".mp4";
		
		File.WriteAllBytes(path, videoData);

		callback(path);
	}

	private IEnumerator Login() {
		WWWForm form = new WWWForm();
		form.AddField("mail_tel", USER_ID);
		form.AddField("password", PASSWORD);

		UnityWebRequest www = null;
		yield return StartCoroutine(InvokeRequest(NICO_LOGIN_URI, null, true, form, response => www = response));

		Dictionary<string, string> headers = www.GetResponseHeaders();

		string setCookieHeader = null;
		try {
			setCookieHeader = headers["set-cookie"];
		} catch(KeyNotFoundException keyNotFoundException) {
			//responseのheaderにset-cookieが存在しない場合の例外処理
			Debug.Log(keyNotFoundException.Message);
		}

		foreach(string cookie in setCookieHeader.Split(new string[] {"user_session="}, StringSplitOptions.None)) {
			if(cookie.StartsWith("user_session")) {
				userSession = cookie.Split(new string[] {";"}, StringSplitOptions.None)[0];
			}
		}
        www.Dispose();
	}

	private IEnumerator CreateHistory() {
		string historyURL = HISTORY_URI + contentId;
		UnityWebRequest www = null;
		yield return StartCoroutine(InvokeRequest(historyURL, GenerateCookie(), false, null, response => www = response));
		Dictionary<string, string> headers = www.GetResponseHeaders();

		string setCookieHeader = null;
		try {
			setCookieHeader = headers["set-cookie"];
		} catch(KeyNotFoundException keyNotFoundException) {
			//ToDo: responseのheaderにset-cookieが存在しない場合の例外処理
			Debug.Log(keyNotFoundException.Message);
		}

		foreach(string cookie in setCookieHeader.Split(new string[] {"nicohistory="}, StringSplitOptions.None)) {
			if(cookie.StartsWith("sm")) history = cookie.Split(';')[0];
		}
		www.Dispose();
	}


	///<summary>
	/// getflvAPIを叩いて動画本体のURLを取得する
	///</summary>
	private IEnumerator GetVideoURL(Action<string> callback) {
		string flvURL = GETFLV_URI + contentId;
		UnityWebRequest www = null;
		yield return StartCoroutine(InvokeRequest(flvURL, GenerateCookie(), false, null, response => www = response));
		string[] body = www.downloadHandler.text.Split('&');
		foreach(string b in body) {
			if(b.StartsWith("url=")) {
				callback(WWW.UnEscapeURL(b));
			}
		}
		www.Dispose();
	}

	///<summary>
	///指定URLからダウンロードしたデータを返す
	///</summary>
	private IEnumerator GetVideo(string videoURL, Action<byte[]> callback) {
		yield return StartCoroutine(InvokeRequest(videoURL, GenerateCookie(), false, null, callback:(www =>  callback(www.downloadHandler.data))));
	}

	///<summary>
	///クラス内のWebRequestを担当する
	///</sumary>
	///ToDo: 別クラスへ切り出すべき
	private IEnumerator InvokeRequest(string url, string cookie, bool isPost, WWWForm form, Action<UnityWebRequest> callback) {
		UnityWebRequest www;
		if(isPost) www = UnityWebRequest.Post(url, form);
		else www = UnityWebRequest.Get(url);

		if(cookie != null) www.SetRequestHeader("Cookie", cookie);

		yield return www.SendWebRequest();

		callback(www);
	}

	///<summary>
	/// UserSessionとNicoHistoryのクッキーをnullでない場合のみ作成して返す
	///</summary>
	private string GenerateCookie() {
		//if(userSession == null) yield return StartCoroutine(NICO_LOGIN_URI());
		string cookie = "user_session=" + userSession +  "; ";
		
		if(history != null) cookie += "nicohistory=" + history + "; ";

		return cookie;	
	}

	///<summary>
	///	渡されたURLを想定した文字列からニコニコのコンテンツID(smXXXX)を抽出する
	///</summary>
	private void ParseContentID(string url) {
		string[] pathes = url.Split('/');
		foreach(string path in pathes) {
			if(path.StartsWith("sm")) contentId = path;
		}
	}

	
}
