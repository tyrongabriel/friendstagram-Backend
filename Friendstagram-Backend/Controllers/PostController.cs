﻿using Friendstagram_Backend.Interfaces;
using Friendstagram_Backend.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Friendstagram_Backend.DTOs;

namespace Friendstagram_Backend.Controllers
{
    [Authorize]
    [EnableCors]
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : FriendstagramControllerBase
    {
        public PostController(FriendstagramContext dbContext, IJwtAuthenticationManager jwtAuthenticationManager) : base(dbContext, jwtAuthenticationManager)
        {

        }

        //GET api/post
        [HttpGet("")]
        public IActionResult GetPost( [FromQuery] int index, [FromQuery] int items = 5)
        {
            try
            {
                User thisUser;
                this.User.GetUser(DBContext, out thisUser, true);

                Group group = DBContext.Groups.Where(group => group.GroupId == thisUser.GroupId).FirstOrDefault();

                List<PostDto> posts = DBContext.Posts.Where(post => post.User.GroupId == group.GroupId).OrderByDescending(p => p.CreatedAt).Select(x => x.AsDto()).ToList();
                
                posts.Take(items);
                List<PostDto> postsToReturn = new List<PostDto>();
                
                for (int i = index; i < posts.Count; i++)
                {
                    postsToReturn.Add(posts[i]);
                }

                return Ok(postsToReturn);
                
            }
            catch (Exception)
            {

                return StatusCode(StatusCodes.Status500InternalServerError, "Something went wrong!");
            }

        }
    }
}
