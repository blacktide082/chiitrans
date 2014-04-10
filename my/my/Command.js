// Generated by CoffeeScript 1.7.1
require(function() {
  var Command;
  return Command = (function() {
    Command.MOVE = 0;

    Command.WAIT = 1;

    Command.CAST = 2;

    Command.GRAB = 3;

    Command.GRABTO = 4;

    Command.USE = 5;

    function Command(id, data) {
      var k, v;
      this.id = id;
      if (data != null) {
        for (k in data) {
          v = data[k];
          this[k] = v;
        }
      }
    }

    return Command;

  })();
});
