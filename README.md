RESERVO - Restaurant Reservation
=================================

Reservo - proof of concept restaurant reservation WPF application.

- Single window application
- Built using DOTNET linq to xml.
- No databases were used. Only XML files, Table.xml reservation.xml

The following assumptions were made while developing this application.

1. The end user of this app is not the person who is going to be using the reservation.
	that is, this application is more for the restaurant conciegre who makes reservations on customer's behalf.

2. The max time between reservation is an hour. i.e, if I make a reservation at 11:00 AM, my reservation ends at 12:00 PM

3. Each party has one and only one table. They cannot share table between other parties.

4. A party cannot have more people in it than the maxoccupancy of the largest table in the restaurant.

5. The comments and error messages will give more detail to the functioning of the application

6. There is no Error Handling code here, that is intentional and to figure out corner cases where errors might appear or try to get the app to crash.
