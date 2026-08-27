import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { apiUrl } from "./api";

export function connectListHub(
  email: string,
  household: string,
  onChanged: () => void
): () => void {
  const connection: HubConnection = new HubConnectionBuilder()
    .withUrl(apiUrl("/hubs/list"))
    .withAutomaticReconnect()
    .build();

  connection.on("listChanged", onChanged);

  let stopped = false;
  void connection
    .start()
    .then(() => {
      if (!stopped) {
        return connection.invoke("JoinHousehold", email, household);
      }
    })
    .catch(() => {
      /* keep the page usable; the next reconnect or reload will retry */
    });

  connection.onreconnected(() => {
    void connection.invoke("JoinHousehold", email, household).catch(() => undefined);
  });

  return () => {
    stopped = true;
    connection.off("listChanged", onChanged);
    void connection.stop();
  };
}
