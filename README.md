1. Загружаем плагины по [ссылке](https://github.com/mcp-servers-for-revit/mcp-servers-for-revit/releases)
2. Содержимое архива копируем в `%AppData%\Autodesk\Revit\Addins\<your Revit version>\`
3. В revit 
	1. Открываем документ
	2. Вкладки "Надстройки"
	3. **Включаем доступные команды.** Settings > Выбираем RevitMCPCommandSet > Выбираем всё и сохраняем.
	4. **Включаем Revit MCP Switch** - поднимаем в Revit сервер который будет слушать команды от Claude
4. Настраиваем MCP в Claude
	1. В терминале (командной строке) запускаем `claude mcp add mcp-server-for-revit -- cmd /c npx -y mcp-server-for-revit`. Предварительно npx нужно установить.
	2. Claude client → File → Setting;  Desktop app → Extensions → Manage → Edit Config → claude_desktop_config.json
		Внутри первых скобок { вставляем
		```json
		"mcpServers": {
		    "mcp-server-for-revit": {
		      "command": "cmd",
		      "args": [
		        "/c",
		        "npx",
		        "-y",
		        "mcp-server-for-revit"
		      ],
		      "env": {
		        "NODE_OPTIONS": "--dns-result-order=ipv4first"
		      }
		    }
		  }
		```
	"env": {
        "NODE_OPTIONS": "--dns-result-order=ipv4first" - не обязательно, но у меня без него были проблемы
5. В чате пишем `Use the say_hello tool` если всё настроено верно, в Revit появится окно сообщения. Если что-то не работает, скидываем Claude путь к папке плагина и спрашиваем что не так.



[Оригинальная инструкция](https://github.com/mcp-servers-for-revit/revit-mcp-plugin)

Полезные сслылки:

тоже самое но через [python](https://github.com/mcp-servers-for-revit/mcp-server-for-revit-python)

https://revitbeh.blogspot.com/2026/06/ai-revit.html

https://dzen.ru/a/aX-Zf2D6OFk538Xx

https://dzen.ru/a/ah8I3XONT01XkYqo
