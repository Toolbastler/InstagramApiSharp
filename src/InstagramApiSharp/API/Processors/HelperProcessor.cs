﻿/*
 * Developer: Ramtin Jokar [ Ramtinak@live.com ] [ My Telegram Account: https://t.me/ramtinak ]
 * 
 * Github source: https://github.com/ramtinak/InstagramApiSharp
 * Nuget package: https://www.nuget.org/packages/InstagramApiSharp
 * 
 * IRANIAN DEVELOPERS
 */
using InstagramApiSharp.Classes;
using InstagramApiSharp.Classes.Android.DeviceInfo;
using InstagramApiSharp.Logger;
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using InstagramApiSharp.Helpers;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using InstagramApiSharp.Converters;
using InstagramApiSharp.Classes.ResponseWrappers;
using InstagramApiSharp.Classes.Models;
using System.Net;
using InstagramApiSharp.Converters.Json;
using InstagramApiSharp.Enums;
using System.IO;

namespace InstagramApiSharp.API.Processors
{
    /// <summary>
    ///     Helper processor for other processors
    /// </summary>
    internal class HelperProcessor
    {
        #region Properties and constructor
        private readonly AndroidDevice _deviceInfo;
        private readonly IHttpRequestProcessor _httpRequestProcessor;
        private readonly IInstaLogger _logger;
        private readonly UserSessionData _user;
        private readonly UserAuthValidate _userAuthValidate;
        private readonly InstaApi _instaApi;
        public HelperProcessor(AndroidDevice deviceInfo, UserSessionData user,
            IHttpRequestProcessor httpRequestProcessor, IInstaLogger logger,
            UserAuthValidate userAuthValidate, InstaApi instaApi)
        {
            _deviceInfo = deviceInfo;
            _user = user;
            _httpRequestProcessor = httpRequestProcessor;
            _logger = logger;
            _userAuthValidate = userAuthValidate;
            _instaApi = instaApi;
        }
        #endregion Properties and constructor

        /// <summary>
        ///     Send video story, direct video, disappearing video
        /// </summary>
        /// <param name="isDirectVideo">Direct video</param>
        /// <param name="isDisappearingVideo">Disappearing video</param>
        public async Task<IResult<bool>> SendVideoAsync(bool isDirectVideo,bool isDisappearingVideo,string caption, 
            InstaViewMode viewMode, InstaStoryType storyType,  string recipients, string threadId, InstaVideoUpload video)
        {
            try
            {
                var uploadId = ApiRequestMessage.GenerateRandomUploadId();
                var videoHashCode = Path.GetFileName(video.Video.Uri).GetHashCode();
                var waterfallId = Guid.NewGuid().ToString();
                var videoEntityName = string.Format("{0}_0_{1}", uploadId, videoHashCode);
                var videoUri = UriCreator.GetStoryUploadVideoUri(uploadId, videoHashCode);
                var retryContext = GetRetryContext();
                HttpRequestMessage request = null;
                HttpResponseMessage response = null;
                string videoUploadParams = null;
                string json = null;
                var videoUploadParamsObj = new JObject();
                if (isDirectVideo)
                {
                    videoUploadParamsObj = new JObject
                    {
                        {"upload_media_height", "0"},
                        {"direct_v2", "1"},
                        {"upload_media_width", "0"},
                        {"upload_media_duration_ms", "0"},
                        {"upload_id", uploadId},
                        {"retry_context", retryContext},
                        {"media_type", "2"}
                    };

                    videoUploadParams = JsonConvert.SerializeObject(videoUploadParamsObj);
                    request = HttpHelper.GetDefaultRequest(HttpMethod.Get, videoUri, _deviceInfo);
                    request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                    request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                    response = await _httpRequestProcessor.SendAsync(request);
                    json = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode != HttpStatusCode.OK)
                        return Result.UnExpectedResponse<bool>(response, json);
                }
                else
                {
                    videoUploadParamsObj = new JObject
                    {
                        {"_csrftoken", _user.CsrfToken},
                        {"_uid", _user.LoggedInUser.Pk},
                        {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                        {"media_info", new JObject
                            {
                                    {"capture_mode", "normal"},
                                    {"media_type", 2},
                                    {"caption", caption ?? string.Empty},
                                    {"mentions", new JArray()},
                                    {"hashtags", new JArray()},
                                    {"locations", new JArray()},
                                    {"stickers", new JArray()},
                            }
                        }
                    };
                    request = HttpHelper.GetSignedRequest(HttpMethod.Post, UriCreator.GetStoryMediaInfoUploadUri(), _deviceInfo, videoUploadParamsObj);
                    response = await _httpRequestProcessor.SendAsync(request);
                    json = await response.Content.ReadAsStringAsync();


                    videoUploadParamsObj = new JObject
                    {
                        {"upload_media_height", "0"},
                        {"upload_media_width", "0"},
                        {"upload_media_duration_ms", "0"},
                        {"upload_id", uploadId},
                        {"retry_context", "{\"num_step_auto_retry\":0,\"num_reupload\":0,\"num_step_manual_retry\":0}"},
                        {"media_type", "2"}
                    };
                    if (isDisappearingVideo)
                    {
                        videoUploadParamsObj.Add("for_direct_story", "1");
                    }
                    else
                    {
                        switch (storyType)
                        {
                            case InstaStoryType.SelfStory:
                            default:
                                videoUploadParamsObj.Add("for_album", "1");
                                break;
                            case InstaStoryType.Direct:
                                videoUploadParamsObj.Add("for_direct_story", "1");
                                break;
                            case InstaStoryType.Both:
                                videoUploadParamsObj.Add("for_album", "1");
                                videoUploadParamsObj.Add("for_direct_story", "1");
                                break;
                        }
                    }
                    videoUploadParams = JsonConvert.SerializeObject(videoUploadParamsObj);
                    request = HttpHelper.GetDefaultRequest(HttpMethod.Get, videoUri, _deviceInfo);
                    request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                    request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                    response = await _httpRequestProcessor.SendAsync(request);
                    json = await response.Content.ReadAsStringAsync();


                    if (response.StatusCode != HttpStatusCode.OK)
                        return Result.UnExpectedResponse<bool>(response, json);
                }

                // video part
                byte[] videoBytes;
                if (video.Video.VideoBytes == null)
                    videoBytes = File.ReadAllBytes(video.Video.Uri);
                else
                    videoBytes = video.Video.VideoBytes;

                var videoContent = new ByteArrayContent(videoBytes);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, videoUri, _deviceInfo);
                request.Content = videoContent;
                var vidExt = Path.GetExtension(video.Video.Uri).Replace(".", "").ToLower();
                if (vidExt == "mov")
                    request.Headers.Add("X-Entity-Type", "video/quicktime");
                else
                    request.Headers.Add("X-Entity-Type", "video/mp4");

                request.Headers.Add("Offset", "0");
                request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                request.Headers.Add("X-Entity-Name", videoEntityName);
                request.Headers.Add("X-Entity-Length", videoBytes.Length.ToString());
                request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();

                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<bool>(response, json);

                if (!isDirectVideo)
                {
                    var photoHashCode = Path.GetFileName(video.VideoThumbnail.Uri).GetHashCode();
                    var photoEntityName = string.Format("{0}_0_{1}", uploadId, photoHashCode);
                    var photoUri = UriCreator.GetStoryUploadPhotoUri(uploadId, photoHashCode);
                    var photoUploadParamsObj = new JObject
                    {
                        {"retry_context", retryContext},
                        {"media_type", "2"},
                        {"upload_id", uploadId},
                        {"image_compression", "{\"lib_name\":\"moz\",\"lib_version\":\"3.1.m\",\"quality\":\"95\"}"},
                    };

                    var photoUploadParams = JsonConvert.SerializeObject(photoUploadParamsObj);
                    byte[] imageBytes;
                    if (video.VideoThumbnail.ImageBytes == null)
                        imageBytes = File.ReadAllBytes(video.VideoThumbnail.Uri);
                    else
                        imageBytes = video.VideoThumbnail.ImageBytes;
                    var imageContent = new ByteArrayContent(imageBytes);
                    imageContent.Headers.Add("Content-Transfer-Encoding", "binary");
                    imageContent.Headers.Add("Content-Type", "application/octet-stream");
                    request = HttpHelper.GetDefaultRequest(HttpMethod.Post, photoUri, _deviceInfo);
                    request.Content = imageContent;
                    request.Headers.Add("X-Entity-Type", "image/jpeg");
                    request.Headers.Add("Offset", "0");
                    request.Headers.Add("X-Instagram-Rupload-Params", photoUploadParams);
                    request.Headers.Add("X-Entity-Name", photoEntityName);
                    request.Headers.Add("X-Entity-Length", imageBytes.Length.ToString());
                    request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                    response = await _httpRequestProcessor.SendAsync(request);
                    json = await response.Content.ReadAsStringAsync();

                }
                return await ConfigureVideo(uploadId, isDirectVideo, isDisappearingVideo,caption, viewMode,storyType, recipients, threadId);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }

        }

        private async Task<IResult<bool>> ConfigureVideo(string uploadId, bool isDirectVideo, bool isDisappearingVideo, string caption,
            InstaViewMode viewMode, InstaStoryType storyType, string recipients, string threadId)
        {
            try
            {

                Uri instaUri = UriCreator.GetDirectConfigVideoUri();
                var retryContext = GetRetryContext();
                var clientContext = Guid.NewGuid().ToString();
                
                if (isDirectVideo)
                {
                    var data = new Dictionary<string, string>()
                    {
                         {"action","send_item"},
                         {"client_context",clientContext.ToString()},
                         {"_csrftoken",_user.CsrfToken},
                         {"video_result",""},
                         {"_uuid",_deviceInfo.DeviceGuid.ToString()},
                         {"upload_id",uploadId}
                    };
                    if (!string.IsNullOrEmpty(recipients))
                        data.Add("recipient_users", $"[[{recipients}]]");
                    else
                        data.Add("thread_ids", $"[{threadId}]");

                    instaUri = UriCreator.GetDirectConfigVideoUri();
                    var request = HttpHelper.GetDefaultRequest(HttpMethod.Post, instaUri, _deviceInfo);
                    request.Content = new FormUrlEncodedContent(data);
                    request.Headers.Add("retry_context", retryContext);
                    var response = await _httpRequestProcessor.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode != HttpStatusCode.OK)
                        return Result.UnExpectedResponse<bool>(response, json);

                    var obj = JsonConvert.DeserializeObject<InstaDefault>(json);
                    return obj.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
                }
                else
                {
                    Random rnd = new Random();
                    var data = new JObject
                    {
                        {"filter_type", "0"},
                        {"timezone_offset", "16200"},
                        {"_csrftoken", _user.CsrfToken},
                        {"client_shared_at", (DateTime.UtcNow.ToUnixTime() - rnd.Next(25,55)).ToString()},
                        {"story_media_creation_date", (DateTime.UtcNow.ToUnixTime() - rnd.Next(50,70)).ToString()},
                        {"media_folder", "Camera"},
                        {"source_type", "4"},
                        {"video_result", ""},
                        {"_uid", _user.LoggedInUser.Pk.ToString()},
                        {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                        {"caption", caption ?? string.Empty},
                        {"date_time_original", DateTime.Now.ToString("yyyy-dd-MMTh:mm:ss-0fffZ")},
                        {"capture_type", "normal"},
                        {"mas_opt_in", "NOT_PROMPTED"},
                        {"upload_id", uploadId},
                        {"client_timestamp", DateTime.UtcNow.ToUnixTime()},
                        {
                            "device", new JObject{
                                {"manufacturer", _deviceInfo.HardwareManufacturer},
                                {"model", _deviceInfo.DeviceModelIdentifier},
                                {"android_release", _deviceInfo.AndroidVer.VersionNumber},
                                {"android_version", _deviceInfo.AndroidVer.APILevel}
                            }
                        },
                        {"length", 0},
                        {
                            "extra", new JObject
                            {
                                {"source_width", 0},
                                {"source_height", 0}
                            }
                        },
                        {"audio_muted", false},
                        {"poster_frame_index", 0},
                    };
                    if (isDisappearingVideo)
                    {
                        data.Add("view_mode", viewMode.ToString().ToLower());
                        data.Add("configure_mode", "2");
                        data.Add("recipient_users", "[]");
                        data.Add("thread_ids", $"[{threadId}]");
                    }
                    else
                    {
                        switch (storyType)
                        {
                            case InstaStoryType.SelfStory:
                            default:
                                data.Add("configure_mode", "1");
                                break;
                            case InstaStoryType.Direct:
                                data.Add("configure_mode", "2");
                                data.Add("view_mode", "replayable");
                                data.Add("recipient_users", "[]");
                                data.Add("thread_ids", $"[{threadId}]");
                                break;
                            case InstaStoryType.Both:
                                data.Add("configure_mode", "3");
                                data.Add("view_mode", "replayable");
                                data.Add("recipient_users", "[]");
                                data.Add("thread_ids", $"[{threadId}]");
                                break;
                        }


                    }
                    instaUri = UriCreator.GetVideoStoryConfigureUri(true);
                    var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                 
                    request.Headers.Add("retry_context", retryContext);
                    var response = await _httpRequestProcessor.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var mediaResponse = JsonConvert.DeserializeObject<InstaDefault>(json);

                        return mediaResponse.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
                    }
                    return Result.UnExpectedResponse<bool>(response, json);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }



        public async Task<IResult<bool>> SendPhotoAsync(bool isDirectPhoto, bool isDisappearingPhoto, string caption, InstaViewMode viewMode, InstaStoryType storyType, string recipients, string threadId, InstaImage image)
        {
            try
            {
                var uploadId = ApiRequestMessage.GenerateRandomUploadId();
                var photoHashCode = Path.GetFileName(image.Uri).GetHashCode();
                var photoEntityName = string.Format("{0}_0_{1}", uploadId, photoHashCode);
                var photoUri = UriCreator.GetStoryUploadPhotoUri(uploadId, photoHashCode);
                var waterfallId = Guid.NewGuid().ToString();
                var retryContext = GetRetryContext();
                HttpRequestMessage request = null;
                HttpResponseMessage response = null;
                string json = null;
                var photoUploadParamsObj = new JObject
                {
                    {"retry_context", retryContext},
                    {"media_type", "1"},
                    {"upload_id", uploadId},
                    {"image_compression", "{\"lib_name\":\"moz\",\"lib_version\":\"3.1.m\",\"quality\":\"95\"}"},
                };
                //if (isDirectPhoto)
                //{
                //}
                //else
                {
                    var uploadParamsObj = new JObject
                    {
                        {"_csrftoken", _user.CsrfToken},
                        {"_uid", _user.LoggedInUser.Pk},
                        {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                        {"media_info", new JObject
                            {
                                    {"capture_mode", "normal"},
                                    {"media_type", 1},
                                    {"caption", caption ?? string.Empty},
                                    {"mentions", new JArray()},
                                    {"hashtags", new JArray()},
                                    {"locations", new JArray()},
                                    {"stickers", new JArray()},
                            }
                        }
                    };
                    request = HttpHelper.GetSignedRequest(HttpMethod.Post, UriCreator.GetStoryMediaInfoUploadUri(), _deviceInfo, uploadParamsObj);
                    response = await _httpRequestProcessor.SendAsync(request);
                    json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine(json);

                    
                    var uploadParams = JsonConvert.SerializeObject(photoUploadParamsObj);
                    request = HttpHelper.GetDefaultRequest(HttpMethod.Get, photoUri, _deviceInfo);
                    request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                    request.Headers.Add("X-Instagram-Rupload-Params", uploadParams);
                    response = await _httpRequestProcessor.SendAsync(request);
                    json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine(json);


                    if (response.StatusCode != HttpStatusCode.OK)
                        return Result.UnExpectedResponse<bool>(response, json);
                }
                
                //if (!isDirectPhoto)
                {
                    var photoUploadParams = JsonConvert.SerializeObject(photoUploadParamsObj);
                    byte[] imageBytes;
                    if (image.ImageBytes == null)
                        imageBytes = File.ReadAllBytes(image.Uri);
                    else
                        imageBytes = image.ImageBytes;
                    var imageContent = new ByteArrayContent(imageBytes);
                    imageContent.Headers.Add("Content-Transfer-Encoding", "binary");
                    imageContent.Headers.Add("Content-Type", "application/octet-stream");
                    request = HttpHelper.GetDefaultRequest(HttpMethod.Post, photoUri, _deviceInfo);
                    request.Content = imageContent;
                    request.Headers.Add("X-Entity-Type", "image/jpeg");
                    request.Headers.Add("Offset", "0");
                    request.Headers.Add("X-Instagram-Rupload-Params", photoUploadParams);
                    request.Headers.Add("X-Entity-Name", photoEntityName);
                    request.Headers.Add("X-Entity-Length", imageBytes.Length.ToString());
                    request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                    response = await _httpRequestProcessor.SendAsync(request);
                    json = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine(json);

                }
                return await ConfigurePhoto(uploadId, isDirectPhoto, isDisappearingPhoto, caption, viewMode, storyType, recipients, threadId);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }

        }

        private async Task<IResult<bool>> ConfigurePhoto(string uploadId, bool isDirectPhoto, bool isDisappearingPhoto, string caption, InstaViewMode viewMode, InstaStoryType storyType, string recipients, string threadId)
        {
            try
            {

                Uri instaUri = UriCreator.GetDirectConfigVideoUri();
                var retryContext = GetRetryContext();
                var clientContext = Guid.NewGuid().ToString();

                //if (isDirectVideo)
                //{
           
                //}
                //else
                {

                    //{
                    //	"recipient_users": "[]",
                    //	"view_mode": "permanent",
                    //	"thread_ids": "[\"340282366841710300949128132202173515958\"]",
                    //	"timezone_offset": "16200",
                    //	"_csrftoken": "gRMgctLzzC9MfJBQTz3MzxeYMtBxCY4s",
                    //	"client_shared_at": "1536323374",
                    //	"configure_mode": "2",
                    //	"source_type": "3",
                    //	"_uid": "7405924766",
                    //	"_uuid": "6324ecb2-e663-4dc8-a3a1-289c699cc876",
                    //	"capture_type": "normal",
                    //	"mas_opt_in": "NOT_PROMPTED",
                    //	"upload_id": "469885239145487",
                    //	"client_timestamp": "1536323328",
                    //	"device": {
                    //		"manufacturer": "HUAWEI",
                    //		"model": "PRA-LA1",
                    //		"android_version": 24,
                    //		"android_release": "7.0"
                    //	},
                    //	"edits": {
                    //		"crop_original_size": [2240.0, 3968.0],
                    //		"crop_center": [0.0, -2.5201612E-4],
                    //		"crop_zoom": 1.0595461
                    //	},
                    //	"extra": {
                    //		"source_width": 2232,
                    //		"source_height": 3745
                    //	}
                    //}
                    Random rnd = new Random();
                    var data = new JObject
                    {
                        {"timezone_offset", "16200"},
                        {"_csrftoken", _user.CsrfToken},
                        {"client_shared_at", (DateTime.UtcNow.ToUnixTime() - rnd.Next(25,55)).ToString()},
                        {"story_media_creation_date", (DateTime.UtcNow.ToUnixTime() - rnd.Next(50,70)).ToString()},
                        {"media_folder", "Camera"},
                        {"source_type", "3"},
                        {"video_result", ""},
                        {"_uid", _user.LoggedInUser.Pk.ToString()},
                        {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                        {"caption", caption ?? string.Empty},
                        {"date_time_original", DateTime.Now.ToString("yyyy-dd-MMTh:mm:ss-0fffZ")},
                        {"capture_type", "normal"},
                        {"mas_opt_in", "NOT_PROMPTED"},
                        {"upload_id", uploadId},
                        {"client_timestamp", DateTime.UtcNow.ToUnixTime()},
                        {
                            "device", new JObject{
                                {"manufacturer", _deviceInfo.HardwareManufacturer},
                                {"model", _deviceInfo.DeviceModelIdentifier},
                                {"android_release", _deviceInfo.AndroidVer.VersionNumber},
                                {"android_version", _deviceInfo.AndroidVer.APILevel}
                            }
                        },
                        {
                            "extra", new JObject
                            {
                                {"source_width", 0},
                                {"source_height", 0}
                            }
                        }
                    };
                    if (isDisappearingPhoto)
                    {
                        data.Add("view_mode", viewMode.ToString().ToLower());
                        data.Add("configure_mode", "2");
                        data.Add("recipient_users", "[]");
                        data.Add("thread_ids", $"[{threadId}]");
                    }
                    else
                    {
                        switch (storyType)
                        {
                            case InstaStoryType.SelfStory:
                            default:
                                data.Add("configure_mode", "1");
                                break;
                            case InstaStoryType.Direct:
                                data.Add("configure_mode", "2");
                                data.Add("view_mode", "replayable");
                                data.Add("recipient_users", "[]");
                                data.Add("thread_ids", $"[{threadId}]");
                                break;
                            case InstaStoryType.Both:
                                data.Add("configure_mode", "3");
                                data.Add("view_mode", "replayable");
                                data.Add("recipient_users", "[]");
                                data.Add("thread_ids", $"[{threadId}]");
                                break;
                        }
                    }
                    instaUri = UriCreator.GetVideoStoryConfigureUri(false);
                    var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);

                    request.Headers.Add("retry_context", retryContext);
                    var response = await _httpRequestProcessor.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var mediaResponse = JsonConvert.DeserializeObject<InstaDefault>(json);

                        return mediaResponse.Status.ToLower() == "ok" ? Result.Success(true) : Result.UnExpectedResponse<bool>(response, json);
                    }
                    return Result.UnExpectedResponse<bool>(response, json);
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<bool>(exception);
            }
        }


        public async Task<IResult<InstaMedia>> SendMediaPhotoAsync(InstaImage image, string caption, InstaLocationShort location)
        {
            try
            {
                var uploadId = ApiRequestMessage.GenerateRandomUploadId();
                var photoHashCode = Path.GetFileName(image.Uri).GetHashCode();
                var photoEntityName = string.Format("{0}_0_{1}", uploadId, photoHashCode);
                var photoUri = UriCreator.GetStoryUploadPhotoUri(uploadId, photoHashCode);
                var waterfallId = Guid.NewGuid().ToString();
                var retryContext = GetRetryContext();
                HttpRequestMessage request = null;
                HttpResponseMessage response = null;
                string json = null;
                var photoUploadParamsObj = new JObject
                {
                    {"retry_context", retryContext},
                    {"media_type", "1"},
                    {"upload_id", uploadId},
                    {"image_compression", "{\"lib_name\":\"moz\",\"lib_version\":\"3.1.m\",\"quality\":\"95\"}"},
                };
                var uploadParamsObj = new JObject
                {
                    {"_csrftoken", _user.CsrfToken},
                    {"_uid", _user.LoggedInUser.Pk},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"media_info", new JObject
                        {
                                {"capture_mode", "normal"},
                                {"media_type", 1},
                                {"caption", caption ?? string.Empty},
                                {"mentions", new JArray()},
                                {"hashtags", new JArray()},
                                {"locations", new JArray()},
                                {"stickers", new JArray()},
                        }
                    }
                };
                request = HttpHelper.GetSignedRequest(HttpMethod.Post, UriCreator.GetStoryMediaInfoUploadUri(), _deviceInfo, uploadParamsObj);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();


                var uploadParams = JsonConvert.SerializeObject(photoUploadParamsObj);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Get, photoUri, _deviceInfo);
                request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                request.Headers.Add("X-Instagram-Rupload-Params", uploadParams);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();


                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaMedia>(response, json);


                var photoUploadParams = JsonConvert.SerializeObject(photoUploadParamsObj);
                byte[] imageBytes;
                if (image.ImageBytes == null)
                    imageBytes = File.ReadAllBytes(image.Uri);
                else
                    imageBytes = image.ImageBytes;
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.Add("Content-Transfer-Encoding", "binary");
                imageContent.Headers.Add("Content-Type", "application/octet-stream");
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, photoUri, _deviceInfo);
                request.Content = imageContent;
                request.Headers.Add("X-Entity-Type", "image/jpeg");
                request.Headers.Add("Offset", "0");
                request.Headers.Add("X-Instagram-Rupload-Params", photoUploadParams);
                request.Headers.Add("X-Entity-Name", photoEntityName);
                request.Headers.Add("X-Entity-Length", imageBytes.Length.ToString());
                request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return await ConfigureMediaPhotoAsync(uploadId, caption, location);
                return Result.UnExpectedResponse<InstaMedia>(response, json);
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaMedia>(exception);
            }

        }

        private async Task<IResult<InstaMedia>> ConfigureMediaPhotoAsync(string uploadId, string caption, InstaLocationShort location)
        {
            try
            {

                Uri instaUri = UriCreator.GetMediaConfigureUri();
                var retryContext = GetRetryContext();
                var clientContext = Guid.NewGuid().ToString();
                Random rnd = new Random();
                var data = new JObject
                {
                    {"timezone_offset", "16200"},
                    {"_csrftoken", _user.CsrfToken},
                    {"client_shared_at", (DateTime.UtcNow.ToUnixTime() - rnd.Next(25,55)).ToString()},
                    {"story_media_creation_date", (DateTime.UtcNow.ToUnixTime() - rnd.Next(50,70)).ToString()},
                    {"media_folder", "Camera"},
                    {"source_type", "3"},
                    {"_uid", _user.LoggedInUser.Pk.ToString()},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"caption", caption ?? string.Empty},
                    {"date_time_original", DateTime.Now.ToString("yyyy-dd-MMTh:mm:ss-0fffZ")},
                    {"capture_type", "normal"},
                    {"mas_opt_in", "NOT_PROMPTED"},
                    {"upload_id", uploadId},
                    {"client_timestamp", DateTime.UtcNow.ToUnixTime()},
                    {"date_time_digitalized", DateTime.Now.ToString("yyyy:dd:MM+h:mm:ss")},
                    {
                        "device", new JObject{
                            {"manufacturer", _deviceInfo.HardwareManufacturer},
                            {"model", _deviceInfo.DeviceModelIdentifier},
                            {"android_release", _deviceInfo.AndroidVer.VersionNumber},
                            {"android_version", _deviceInfo.AndroidVer.APILevel}
                        }
                    },
                    {
                        "extra", new JObject
                        {
                            {"source_width", 0},
                            {"source_height", 0}
                        }
                    }
                };
                if (location != null)
                {
                    data.Add("location", location.GetJson());
                }

                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);
                request.Headers.Add("retry_context", retryContext);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(json);
                var mediaResponse =
                     JsonConvert.DeserializeObject<InstaMediaItemResponse>(json, new InstaMediaDataConverter());
                var converter = ConvertersFabric.Instance.GetSingleMediaConverter(mediaResponse);
                return Result.Success(converter.Convert());
            }
            catch (Exception exception)
            {
                _logger?.LogException(exception);
                return Result.Fail<InstaMedia>(exception);
            }
        }





        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////// INSTAGRAM TV ////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public async Task<IResult<InstaMedia>> SendIGTVVideoAsync(InstaVideoUpload video, string title, string caption)
        {
            try
            {
                var uploadId = ApiRequestMessage.GenerateRandomUploadId();
                var videoHashCode = Path.GetFileName(video.Video.Uri).GetHashCode();
                var waterfallId = Guid.NewGuid().ToString();
                var videoEntityName = string.Format("{0}_0_{1}", uploadId, videoHashCode);
                var videoUri = UriCreator.GetStoryUploadVideoUri(uploadId, videoHashCode);
                var retryContext = GetRetryContext();
                HttpRequestMessage request = null;
                HttpResponseMessage response = null;
                string videoUploadParams = null;
                string json = null;
                var videoUploadParamsObj = new JObject();
                videoUploadParamsObj = new JObject
                    {
                        {"_csrftoken", _user.CsrfToken},
                        {"_uid", _user.LoggedInUser.Pk},
                        {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                        {"media_info", new JObject
                            {
                                    {"capture_mode", "normal"},
                                    {"media_type", 2},
                                    {"caption", caption ?? string.Empty},
                                    {"mentions", new JArray()},
                                    {"hashtags", new JArray()},
                                    {"locations", new JArray()},
                                    {"stickers", new JArray()},
                            }
                        }
                    };
                request = HttpHelper.GetSignedRequest(HttpMethod.Post, UriCreator.GetStoryMediaInfoUploadUri(), _deviceInfo, videoUploadParamsObj);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(json);

                videoUploadParamsObj = new JObject
                    {
                        {"upload_media_height", "0"},
                        {"upload_media_width", "0"},
                        {"upload_media_duration_ms", "0"},
                        {"upload_id", uploadId},
                        {"retry_context", "{\"num_step_auto_retry\":0,\"num_reupload\":0,\"num_step_manual_retry\":0}"},
                        {"media_type", "2"},
                        {"is_igtv_video", "1"}
                    };

                videoUploadParams = JsonConvert.SerializeObject(videoUploadParamsObj);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Get, videoUri, _deviceInfo);
                request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(json);

                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaMedia>(response, json);


                // video part
                byte[] videoBytes;
                if (video.Video.VideoBytes == null)
                    videoBytes = File.ReadAllBytes(video.Video.Uri);
                else
                    videoBytes = video.Video.VideoBytes;

                var videoContent = new ByteArrayContent(videoBytes);
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, videoUri, _deviceInfo);
                request.Content = videoContent;
                var vidExt = Path.GetExtension(video.Video.Uri).Replace(".", "").ToLower();
                if (vidExt == "mov")
                    request.Headers.Add("X-Entity-Type", "video/quicktime");
                else
                    request.Headers.Add("X-Entity-Type", "video/mp4");

                request.Headers.Add("Offset", "0");
                request.Headers.Add("X-Instagram-Rupload-Params", videoUploadParams);
                request.Headers.Add("X-Entity-Name", videoEntityName);
                request.Headers.Add("X-Entity-Length", videoBytes.Length.ToString());
                request.Headers.Add("X_FB_VIDEO_WATERFALL_ID", waterfallId);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(json);

                if (response.StatusCode != HttpStatusCode.OK)
                    return Result.UnExpectedResponse<InstaMedia>(response, json);

                var photoHashCode = Path.GetFileName(video.VideoThumbnail.Uri).GetHashCode();
                var photoEntityName = string.Format("{0}_0_{1}", uploadId, photoHashCode);
                var photoUri = UriCreator.GetStoryUploadPhotoUri(uploadId, photoHashCode);
                var photoUploadParamsObj = new JObject
                    {
                        {"retry_context", retryContext},
                        {"media_type", "2"},
                        {"upload_id", uploadId},
                        {"image_compression", "{\"lib_name\":\"moz\",\"lib_version\":\"3.1.m\",\"quality\":\"95\"}"},
                    };

                var photoUploadParams = JsonConvert.SerializeObject(photoUploadParamsObj);
                byte[] imageBytes;
                if (video.VideoThumbnail.ImageBytes == null)
                    imageBytes = File.ReadAllBytes(video.VideoThumbnail.Uri);
                else
                    imageBytes = video.VideoThumbnail.ImageBytes;
                var imageContent = new ByteArrayContent(imageBytes);
                imageContent.Headers.Add("Content-Transfer-Encoding", "binary");
                imageContent.Headers.Add("Content-Type", "application/octet-stream");
                request = HttpHelper.GetDefaultRequest(HttpMethod.Post, photoUri, _deviceInfo);
                request.Content = imageContent;
                request.Headers.Add("X-Entity-Type", "image/jpeg");
                request.Headers.Add("Offset", "0");
                request.Headers.Add("X-Instagram-Rupload-Params", photoUploadParams);
                request.Headers.Add("X-Entity-Name", photoEntityName);
                request.Headers.Add("X-Entity-Length", imageBytes.Length.ToString());
                request.Headers.Add("X_FB_PHOTO_WATERFALL_ID", waterfallId);
                response = await _httpRequestProcessor.SendAsync(request);
                json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine(json);
                if (response.IsSuccessStatusCode)
                    return await ConfigureIGTVVideo(uploadId, title, caption);
                return Result.UnExpectedResponse<InstaMedia>(response, json);            
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<InstaMedia>(exception);
            }

        }

        private async Task<IResult<InstaMedia>> ConfigureIGTVVideo(string uploadId, string title,  string caption)
        {
            try
            {

                Uri instaUri = UriCreator.GetMediaConfigureToIGTVUri();
                var retryContext = GetRetryContext();
                var clientContext = Guid.NewGuid().ToString();

                Random rnd = new Random();
                var data = new JObject
                {
                    {"filter_type", "0"},
                    {"_csrftoken", _user.CsrfToken},
                    {"source_type", "4"},
                    {"_uid", _user.LoggedInUser.Pk.ToString()},
                    {"_uuid", _deviceInfo.DeviceGuid.ToString()},
                    {"caption", caption ?? string.Empty},
                    {"upload_id", uploadId},
                    {
                        "device", new JObject{
                            {"manufacturer", _deviceInfo.HardwareManufacturer},
                            {"model", _deviceInfo.DeviceModelIdentifier},
                            {"android_release", _deviceInfo.AndroidVer.VersionNumber},
                            {"android_version", _deviceInfo.AndroidVer.APILevel}
                        }
                    },
                    {"length", 0},
                    {
                        "extra", new JObject
                        {
                            {"source_width", 0},
                            {"source_height", 0}
                        }
                    },
                    {"audio_muted", false},
                    {"poster_frame_index", 0},
                    {"title", title ?? "" }
                };
                var request = HttpHelper.GetSignedRequest(HttpMethod.Post, instaUri, _deviceInfo, data);

                request.Headers.Add("retry_context", retryContext);
                var response = await _httpRequestProcessor.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                // igtv:
                //{"message": "Transcode error: Video's aspect ratio is too large 1.3333333333333", "status": "fail"}
                //{"message": "Transcode error: Video's aspect ratio is too large 1.7777777777778", "status": "fail"}
                //{"message": "Uploaded image isn't in an allowed aspect ratio", "status": "fail"}
                //{"media": {"taken_at": 1536588655, "pk": 1865362680669764409, "id": "1865362680669764409_1647718432", "device_timestamp": 153658858130102, "media_type": 2, "code": "BnjGXmWl3s5", "client_cache_key": "MTg2NTM2MjY4MDY2OTc2NDQwOQ==.2", "filter_type": 0, "comment_likes_enabled": false, "comment_threading_enabled": false, "has_more_comments": false, "max_num_visible_preview_comments": 2, "preview_comments": [], "can_view_more_preview_comments": false, "comment_count": 0, "product_type": "igtv", "nearly_complete_copyright_match": false, "image_versions2": {"candidates": [{"width": 1080, "height": 1680, "url": "https://scontent-lga3-1.cdninstagram.com/vp/59b658bc87fac07bfb12fc493d810147/5B990274/t51.2885-15/e35/40958056_2159975094323981_8136119356155744850_n.jpg?se=7\u0026ig_cache_key=MTg2NTM2MjY4MDY2OTc2NDQwOQ%3D%3D.2"}, {"width": 240, "height": 373, "url": "https://scontent-lga3-1.cdninstagram.com/vp/524297318efe8ac05afbe7c267673f33/5B98BF5D/t51.2885-15/e35/p240x240/40958056_2159975094323981_8136119356155744850_n.jpg?ig_cache_key=MTg2NTM2MjY4MDY2OTc2NDQwOQ%3D%3D.2"}]}, "original_width": 1080, "original_height": 1680, "thumbnails": {}, "video_versions": [{"type": 101, "width": 480, "height": 746, "url": "https://scontent-lga3-1.cdninstagram.com/vp/04d231154d0d1c95289445a348f26bde/5B98AC98/t50.16885-16/10000000_232772710752607_772643665699930112_n.mp4", "id": "17962568050122118"}, {"type": 103, "width": 480, "height": 746, "url": "https://scontent-lga3-1.cdninstagram.com/vp/04d231154d0d1c95289445a348f26bde/5B98AC98/t50.16885-16/10000000_232772710752607_772643665699930112_n.mp4", "id": "17962568050122118"}, {"type": 102, "width": 480, "height": 746, "url": "https://scontent-lga3-1.cdninstagram.com/vp/04d231154d0d1c95289445a348f26bde/5B98AC98/t50.16885-16/10000000_232772710752607_772643665699930112_n.mp4", "id": "17962568050122118"}], "has_audio": true, "video_duration": 122.669, "user": {"pk": 1647718432, "username": "kajokoleha", "full_name": "kajokoleha", "is_private": false, "profile_pic_url": "https://scontent-lga3-1.cdninstagram.com/vp/82572ce26b79cec0394c295ff1b486b7/5C203459/t51.2885-19/s150x150/29094366_375967546140243_535690319979610112_n.jpg", "profile_pic_id": "1746518311616597634_1647718432", "has_anonymous_profile_picture": false, "can_boost_post": false, "can_see_organic_insights": false, "show_insights_terms": false, "reel_auto_archive": "on", "is_unpublished": false, "allowed_commenter_type": "any"}, "caption": {"pk": 17977422871018862, "user_id": 1647718432, "text": "captioooooooooooooooooooon", "type": 1, "created_at": 1536588656, "created_at_utc": 1536588656, "content_type": "comment", "status": "Active", "bit_flags": 0, "user": {"pk": 1647718432, "username": "kajokoleha", "full_name": "kajokoleha", "is_private": false, "profile_pic_url": "https://scontent-lga3-1.cdninstagram.com/vp/82572ce26b79cec0394c295ff1b486b7/5C203459/t51.2885-19/s150x150/29094366_375967546140243_535690319979610112_n.jpg", "profile_pic_id": "1746518311616597634_1647718432", "has_anonymous_profile_picture": false, "can_boost_post": false, "can_see_organic_insights": false, "show_insights_terms": false, "reel_auto_archive": "on", "is_unpublished": false, "allowed_commenter_type": "any"}, "did_report_as_spam": false, "media_id": 1865362680669764409}, "title": "ramtin vid e o", "caption_is_edited": false, "photo_of_you": false, "can_viewer_save": true, "organic_tracking_token": "eyJ2ZXJzaW9uIjo1LCJwYXlsb2FkIjp7ImlzX2FuYWx5dGljc190cmFja2VkIjpmYWxzZSwidXVpZCI6IjgyYzkyZjU0Y2EyMzRhNjM5YzBiOTBlZDAzODcwODlhMTg2NTM2MjY4MDY2OTc2NDQwOSIsInNlcnZlcl90b2tlbiI6IjE1MzY1ODg2NTc4Mzd8MTg2NTM2MjY4MDY2OTc2NDQwOXwxNjQ3NzE4NDMyfGMwZjkxNmNmMjk2NTU4NzQ1MWRlZmU3NTY2NjY3ZDdiNjE4OTMxYjM3NTQ0YjdhYjg1NmUxYWEwZjhmMmM4MWIifSwic2lnbmF0dXJlIjoiIn0="}, "upload_id": "153658858130102", "status": "ok"}

                if (response.IsSuccessStatusCode)
                {
                    var mediaResponse =
                                      JsonConvert.DeserializeObject<InstaMediaItemResponse>(json, new InstaMediaDataConverter());
                    var converter = ConvertersFabric.Instance.GetSingleMediaConverter(mediaResponse);
                    return Result.Success(converter.Convert());
                }
                return Result.UnExpectedResponse<InstaMedia>(response, json);

            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                _logger?.LogException(exception);
                return Result.Fail<InstaMedia>(exception);
            }
        }






        private string GetRetryContext()
        {
            return new JObject
                {
                    {"num_step_auto_retry", 0},
                    {"num_reupload", 0},
                    {"num_step_manual_retry", 0}
                }.ToString(Formatting.None);
        }
    }
}
