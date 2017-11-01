<div class="userlogin-box">
	<div class="box-header">
		<h3>Login</h3>
	</div>
	<div class="box-content">
		<form action="/providers/users/" method="POST" id="user_login"
		name="user_login" enctype="application/x-www-form-urlencoded">

			<label for="username">Benutzername: </label>
			<br />
			<input type="input" name="username" id="username" class="input_text" width="100%" />

			<br />
			<br />

			<label for="password">Password: </label>
			<br />
			<input type="password" name="password" id="password" class="input_text" width="100%" />
			<br />
			<br />
			<input type="submit" value="Einloggen" onclick="return sendForm('/providers/users/','.userlogin-box','.userlogin-box','#user_login','login', '', '')" />
			
		</form>
		<a href="#" onclick="LoadDocument('/providers/users/','#content','Registrieren','register','', '')">Registrieren</a>
	</div>
</div>
