﻿using Friendstagram_Backend.DTOs;
using Friendstagram_Backend.Interfaces;
using Friendstagram_Backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Web;
using System.IO;
using Microsoft.AspNetCore.Cors;
using System.Net.Mail;
using System.Configuration;

namespace Friendstagram_Backend.Controllers
{
    [Authorize]
    [EnableCors]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : FriendstagramControllerBase
    {
        private SecurityHelper SecurityManager;
        public UserController(FriendstagramContext dbContext, IJwtAuthenticationManager jwtAuthenticationManager, SecurityHelper securityManager) : base(dbContext, jwtAuthenticationManager)
        {
            SecurityManager = securityManager;
        }

        // GET api/user/{username}
        [HttpGet("{username}")]
        public IActionResult GetUser(string username)
        {
            User thisUser;
            this.User.GetUser(DBContext, out thisUser);

            User foundUser = DBContext.Users.FirstOrDefault(u => u.Username == username && u.GroupId == thisUser.GroupId);
            if (foundUser == null)
            {
                return NotFound($"Could not find a user with the username \"{username}\" in your group");
            }
            return Ok(foundUser.AsDto());
        }

        // GET api/user?authorized=true
        [HttpGet]
        public IActionResult GetUsers([FromQuery]bool authorized = false)
        {
            User thisUser;
            User.GetUser(DBContext, out thisUser);
            if (authorized)
            {
                if (thisUser == null)
                {
                    return Unauthorized("Could not find user associated with your authentication!");
                }
                return Ok(thisUser.AsDto());
            }
            else
            {
                List<UserDto> users = DBContext.Users.Where(u => u.GroupId == thisUser.GroupId).Select(u => u.AsDto()).ToList();
                return Ok(users);
            }
        }

        // PATCH api/user/changeProfilePicture/{username}
        [HttpPatch("changeProfilePicture/{username}")]
        public IActionResult ChangeProfilePicture(IFormFile image, string username)
        {
            User thisUser;
            User.GetUser(DBContext, out thisUser, true);
            if (thisUser.Username == username)
            {
                Resource imageResource = image.AsResource(DBContext);
                DBContext.Resources.Add(imageResource);
                DBContext.SaveChanges();
                imageResource.Filename = imageResource.ResourceId + image.FileName;
                imageResource.Path = "resources/users/" + imageResource.Filename;

                StorageManager.DeleteFile("wwwroot/" + thisUser.ProfilePicture.Path);

                thisUser.ProfilePicture = null;
                thisUser.ProfilePictureId = imageResource.ResourceId;
                thisUser.ProfilePicture = imageResource;
                DBContext.SaveChanges();

                var stream = image.OpenReadStream();
                StorageManager.SaveFile(StorageManager.ImagePath + $"users/{imageResource.Filename}", stream);

                DBContext.SaveChanges();
            }
            return Ok(thisUser.AsDto());
        }

        // PATCH api/user/changeUsername/{username}
        [HttpPatch("changeUsername/{username}")]
        public IActionResult ChangeUsername([FromBody] ChangeUsernameDto changeUser, string username)
        {
            User thisUser;
            this.User.GetUser(DBContext, out thisUser, true);
            if (thisUser.Username == username)
            {
                thisUser.Username = changeUser.usernameNew;
                DBContext.SaveChanges();

                UserDto newUserDto = thisUser.AsDto(); // DBContext.Users.Include(u => u.ProfilePicture).FirstOrDefault(u => u.UserId == newUser.UserId).AsDto();

                return Ok(newUserDto);
            }
            else
            {
                return Unauthorized("Unable to change username of another user!");
            }
        }

        // PATCH api/user/changePassword/{username}
        [HttpPatch("changePassword/{username}")]
        public IActionResult ChangePassword([FromBody] ChangePasswordDto changePassword, string username)
        {
            User thisUser;
            this.User.GetUser(DBContext, out thisUser, true);
            if (thisUser.Username == username)
            {
                if (thisUser.Password == SecurityManager.CreateSha256Hash(changePassword.password, thisUser.Salt))
                {
                    string newSalt = SecurityManager.CreateSalt();
                    string hashedPassword = SecurityManager.CreateSha256Hash(changePassword.newPassword, newSalt);

                    thisUser.Password = hashedPassword;
                    thisUser.Salt = newSalt;

                    DBContext.SaveChanges();

                    UserDto newUserDto = thisUser.AsDto();
                    return Ok(newUserDto);
                }
                else
                {
                    return Unauthorized("Wrong Password!");
                }
            }
            else
            {
                return Unauthorized("Unable to change username of another user!");
            }
        }

        // POST api/user/authenticate
        [AllowAnonymous]
        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody] AuthenticationDto userCredits)
        {            
            var token = JwtAuthenticationManager.Authenticate(userCredits.Email, userCredits.Password);

            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized();
            }

            return Ok(new JWTTokenDto() { token = token });
        }

        // POST api/user
        [AllowAnonymous]
        [HttpPost()]
        public ActionResult<UserDto> Register([FromBody] RegisterDto userCredits)
        {
            Group registerIntoGroup = DBContext.Groups.FirstOrDefault(g => g.Code == userCredits.groupCode);

            if (registerIntoGroup == null)
            {
                return NotFound("Could not find Group with code: " + userCredits.groupCode);
            }
            else if (DBContext.Users.Any(u => u.Email == userCredits.email))
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, "User with that email already exists");
            }
            else if (DBContext.Users.Any(u => u.Username == userCredits.username))
            {
                return StatusCode(StatusCodes.Status422UnprocessableEntity, "User with that username already exists");
            }

            string salt = SecurityManager.CreateSalt();
            string verificationCode = SecurityManager.CreateVerifcationCode();
            User RegisteredUser = new User()
            {
                Username = userCredits.username,
                Email = userCredits.email,
                Password = SecurityManager.CreateSha256Hash(userCredits.password, salt),
                Salt = salt,
                GroupId = registerIntoGroup.GroupId,
                VerificationCode = verificationCode,
                ProfilePictureId = 1,
                Verified = 0
                
            };


            DBContext.Users.Add(RegisteredUser);
            DBContext.SaveChanges();

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(ConfigurationManager.AppSettings["Email"], ConfigurationManager.AppSettings["Email Password"]),
                EnableSsl = true,
            };

            smtpClient.Send(ConfigurationManager.AppSettings["Email"], userCredits.email, "Verification Code", "Please verify your Friendstagram Account by clicking on the following link: https://localhost:44372/api/user/verify/" + verificationCode);


            return CreatedAtAction(nameof(GetUser), new { userCredits.username }, DBContext.Users.Include(u => u.ProfilePicture).FirstOrDefault(u => u.UserId == RegisteredUser.UserId).AsDto());
        }

        // GET api/user/{code}
        [AllowAnonymous]
        [HttpGet("verify/{code}")]
        public ActionResult<UserDto> Verify(string code)
        {
            User userToUpdate = DBContext.Users.Where(u => u.VerificationCode == code).FirstOrDefault();

            if (userToUpdate == null) return NotFound();

            userToUpdate.Verified = 1;
            DBContext.SaveChanges();

            return Ok();
        }
     }
}
