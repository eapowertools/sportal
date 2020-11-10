tags = ["btn-north", "btn-south", "btn-executive", "btn-supplychain", "btn-marketing", "btn-qlikdeveloper", "btn-it", "btn-external", "btn-sales"]

function chipShowHide(e) {
  tags.forEach(element => {
    if (e.path[0].classList[3] != element) {
      $(`.${element}`).css("opacity", 0.4);
    } else {
      $(`.${element}`).css("opacity", 1);
    }
  })
}

function pageLoaded() {
  // populate certificate field.
  $.get("./jwt/certificate").done(function (data) {
    document.getElementById("certInfoTextarea").value = data;
  })
}

function copyCertificate() {
  /* Get the text field */
  var copyText = document.getElementById("certInfoTextarea");

  /* Select the text field */
  copyText.select();
  copyText.setSelectionRange(0, 99999); /*For mobile devices*/

  /* Copy the text inside the text field */
  document.execCommand("copy");

  /* Alert the copied text */
  alert("Copied the text: " + copyText.value);
}

function login(user) {
  var hostname = "https://jesseparis.us.qlikcloud.com";
  var webIntegrationID = "vXkk89R3iLz74ft1LJo-L5tV9F7gDMc1";

  $.get("./jwt/token", {
    user: user
  }).done(function (data) {
    return fetch(hostname + "/login/jwt-session?qlik-web-integration-id=" + webIntegrationID, {
      method: 'POST',
      credentials: 'include',
      mode: 'cors',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + data,
        'qlik-web-integration-id': webIntegrationID
      },
      rejectUnauthorized: false
    }).then(function () {
      window.open(hostname, "_blank");
    });
  });
}