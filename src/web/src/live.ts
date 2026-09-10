import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import { apiUrl } from "./api";

export async function connectList(
  email: string,
  household: string,
  pin: string,
  onChange: () => void
): Promise<HubConnection> {
  const connection = new HubConnectionBuilder().withUrl(apiUrl("/hubs/list")).withAutomaticReconnect().build();
  connection.on("listChanged", onChange);
  connection.onreconnected(() => {
    void connection.invoke("Join", email, household, pin);
    onChange();
  });
  await connection.start();
  await connection.invoke("Join", email, household, pin);
  return connection;
}
