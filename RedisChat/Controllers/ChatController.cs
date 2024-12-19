using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;

namespace RedisChat.Controllers
{
    public class ChatController : Controller
    {
        private static readonly ConnectionMultiplexer _muxer = ConnectionMultiplexer.Connect(
                new ConfigurationOptions
                {
                    EndPoints = { { "redis-10768.c323.us-east-1-2.ec2.redns.redis-cloud.com", 10768 } },
                    User = "default",
                    Password = "qvLRaiX3VlR5labk4EjUGNIVZZpbsG5e"
                });

        private static IDatabase RedisDb => _muxer.GetDatabase();

        public ActionResult Index()
        {
            try
            {
                RedisDb.StringSet("testKey", "testValue");
                var testValue = RedisDb.StringGet("testKey");
                if (testValue != "testValue")
                {
                    throw new Exception("Redis test value mismatch.");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Redis connection failed: {ex.Message}";
            }

            return View();
        }

        [HttpPost]
        public JsonResult CreateChannel([FromBody] System.Text.Json.JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("channelName", out var channelNameElement))
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                string channelName = channelNameElement.GetString();

                if (string.IsNullOrWhiteSpace(channelName))
                {
                    return Json(new { success = false, message = "Channel name is invalid." });
                }

                if (RedisDb.KeyType("channels") != RedisType.List)
                {
                    RedisDb.KeyDelete("channels");
                }

                var existingChannels = RedisDb.ListRange("channels", 0, -1).Select(r => r.ToString()).ToList();
                if (existingChannels.Contains(channelName))
                {
                    return Json(new { success = false, message = "Channel already exists." });
                }

                RedisDb.ListLeftPush("channels", channelName);
                return Json(new { success = true, channelName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        public JsonResult GetChannels()
        {
            try
            {
                if (RedisDb.KeyType("channels") != RedisType.List)
                {
                    return Json(new { success = false, message = "The 'channels' key is not a list or does not exist." });
                }
                var channels = RedisDb.ListRange("channels", 0, -1).Select(r => r.ToString()).ToList();
                return Json(channels);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
        [HttpPost]
        public ActionResult PublishMessage([FromBody] JsonElement data)
        {
            try
            {
                string channelName = data.GetProperty("channelName").GetString();
                string message = data.GetProperty("message").GetString();

                if (!string.IsNullOrWhiteSpace(channelName) && !string.IsNullOrWhiteSpace(message))
                {
                    RedisDb.ListRightPush(channelName, message);

                    var subscriber = _muxer.GetSubscriber();
                    subscriber.Publish(channelName, message);

                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Invalid channel or message" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public ActionResult Subscribe(string channelName)
        {
            return Json(new { success = true });
        }
    }
}
