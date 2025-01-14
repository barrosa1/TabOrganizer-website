﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TabOrganizer_website.Data;
using TabOrganizer_website.Dtos;
using TabOrganizer_website.Helpers;
using TabOrganizer_website.Models;
using TabOrganizer_website.Services;

namespace TabOrganizer_website.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IMapper _mapper;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, IMapper mapper, ILogger<UserController> logger)
        {
            _userService = userService;
            _mapper = mapper;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("authenticate", Name = nameof(Authenticate))]
        public async Task<IActionResult> Authenticate([FromBody] UserDto userDto)
        {
            _logger.LogDebug(1, "Authenticate started from UserController");
            try
            {
                var user = await _userService.Authenticate(userDto.Username, userDto.Password);

                if (user == null)
                    return BadRequest(new { message = "Username or password is incorrect" });

                _logger.LogDebug(1, "Authenticate ended in UserController");

                return Ok(new //return user info w/o password
                {
                    Id = user.Id,
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Role = user.Role,
                    Token = user.Token
                });
            }
            catch (AuthenticationException ex)
            {
                _logger.LogError(1, "AuthenticationException has occurred");
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost("register", Name = nameof(Register))]
        public async Task<IActionResult> Register([FromBody] UserDto userDto)
        {
            var user = _mapper.Map<User>(userDto);
            try
            {
                await _userService.Create(user, userDto.Password);
                if(!_userService.Save().Result)
                {
                    _logger.LogError(1, "Exception has occurred with message 'Creating a user failed on save.'");
                    throw new Exception("Creating a user failed on save.");
                }
                return Ok();
            }
            catch (RegisterException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/users
        [Authorize(Roles = Role.Admin)]
        [HttpGet(Name = nameof(GetAllUsers))]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAll();

            if (users != null)
                return Ok(_mapper.Map<IEnumerable<UserDto>>(users));

            _logger.LogDebug(1, "Users not found");
            return NotFound();
        }

        // GET api/users/5
        [HttpGet("{id}", Name = nameof(GetUserById))]
        public async Task<IActionResult> GetUserById(int id)
        {
            var currentUserId = int.Parse(User.Identity.Name);
            if (id != currentUserId && !User.IsInRole(Role.Admin))
                return Forbid();

            var user = await _userService.GetById(id);

            if (user != null)
                return Ok(_mapper.Map<UserDto>(user));

            return NotFound();
        }

        // PUT api/users/5
        [HttpPut("{id}", Name = nameof(UpdateUser))]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UserUpdateDto userDto)
        {
            var currentUserId = int.Parse(User.Identity.Name);
            if (id != currentUserId && !User.IsInRole(Role.Admin))
            {
                _logger.LogError(1, "Updating user is forbidden");
                return Forbid();
            }

            var userFromDb = await _userService.GetById(id);
            if (userFromDb == null)
                return NotFound();
            try
            {
                _mapper.Map(userDto, userFromDb); //could be update in itself w/o password update

                await _userService.Update(userFromDb, userDto.Password); //updates also password
                await _userService.Save();

                return NoContent();
            }
            catch (Exception e)
            {
                return BadRequest(e);
            }


        }

        // DELETE api/users/5
        [Authorize(Roles = Role.Admin)]
        [HttpDelete("{id}", Name = nameof(DeleteUser))]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _userService.GetById(id);

            if (user == null)
                return NotFound();

            await _userService.Delete(id);
            if (!await _userService.Save())//????
            {
                _logger.LogError(1, "Deleting user failed on save");
                throw new Exception("Deleting user failed on save.");
            }

            _logger.LogDebug(1, "User deleted");
            return NoContent();

        }
    }
}
